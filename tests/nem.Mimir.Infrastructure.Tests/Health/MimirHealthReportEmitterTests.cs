using Microsoft.Extensions.Logging;
using nem.Contracts.ControlPlane;
using nem.Contracts.Sentinel;
using nem.Mimir.Infrastructure.Health;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Wolverine;

namespace nem.Mimir.Infrastructure.Tests.Health;

public sealed class MimirHealthReportEmitterTests
{
    [Fact]
    public async Task GetHealthReportIntervalAsync_UsesConfiguredValue()
    {
        var configManager = Substitute.For<IConfigurationManager>();
        configManager.GetConfigAsync("nem.Mimir", "Sentinel:HealthReportIntervalSeconds", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<string?>("15"));

        var emitter = CreateEmitter(configManager: configManager);

        var interval = await emitter.GetHealthReportIntervalAsync(TestContext.Current.CancellationToken);

        interval.ShouldBe(TimeSpan.FromSeconds(15));
    }

    [Fact]
    public void BuildHealthReport_PopulatesExpectedFields()
    {
        var emitter = CreateEmitter();

        var report = emitter.BuildHealthReport(
            processedCount: 96,
            failedCount: 4,
            memoryUsageMb: 512.25,
            cpuPercent: 12.5,
            activeConnections: 3,
            queueDepth: 7,
            memoryLimitMb: 2048);

        report.ServiceName.ShouldBe("nem.Mimir");
        report.Status.ShouldBe(ServiceHealthStatus.Healthy);
        report.ActiveConnections.ShouldBe(3);
        report.QueueDepth.ShouldBe(7);
        report.MemoryUsageMb.ShouldBe(512.25);
        report.CpuPercent.ShouldBe(12.5);
        report.ErrorRate.ShouldBe(4);
        report.Metrics["processed_messages"].ShouldBe(96);
        report.Metrics["failed_messages"].ShouldBe(4);
        report.Metrics["error_rate"].ShouldBe(4);
        report.CustomDiagnostics["service"].ShouldBe("nem.Mimir");
        report.CustomDiagnostics["memory_limit_mb"].ShouldBe("2048");
    }

    [Theory]
    [InlineData(1, 99, 1500, ServiceHealthStatus.Healthy)]
    [InlineData(10, 90, 1500, ServiceHealthStatus.Degraded)]
    [InlineData(25, 75, 1500, ServiceHealthStatus.Unhealthy)]
    public void DetermineStatus_UsesConfiguredThresholds(
        long failedCount,
        long processedCount,
        double memoryUsageMb,
        ServiceHealthStatus expected)
    {
        var status = MimirHealthReportEmitter.DetermineStatus(
            processedCount,
            failedCount,
            memoryUsageMb,
            memoryLimitMb: 2048);

        status.ShouldBe(expected);
    }

    [Fact]
    public async Task PublishHealthReportOnceAsync_WhenFirstPublishFails_ContinuesRunning()
    {
        var bus = Substitute.For<IMessageBus>();
        var callCount = 0;
        bus.PublishAsync(Arg.Any<ServiceHealthReport>())
            .Returns(_ =>
            {
                callCount++;
                if (callCount == 1)
                {
                    throw new InvalidOperationException("publish failed");
                }

                return ValueTask.CompletedTask;
            });

        var emitter = CreateEmitter(messageBus: bus);

        await emitter.PublishHealthReportOnceAsync(TestContext.Current.CancellationToken);
        await emitter.PublishHealthReportOnceAsync(TestContext.Current.CancellationToken);

        await bus.Received(2).PublishAsync(Arg.Any<ServiceHealthReport>());
    }

    private static MimirHealthReportEmitter CreateEmitter(
        IMessageBus? messageBus = null,
        IConfigurationManager? configManager = null)
    {
        var serviceProvider = Substitute.For<IServiceProvider>();
        if (configManager is not null)
        {
            serviceProvider.GetService(typeof(IConfigurationManager)).Returns(configManager);
        }

        return new MimirHealthReportEmitter(
            serviceProvider,
            messageBus ?? Substitute.For<IMessageBus>(),
            Substitute.For<ILogger<MimirHealthReportEmitter>>());
    }
}
