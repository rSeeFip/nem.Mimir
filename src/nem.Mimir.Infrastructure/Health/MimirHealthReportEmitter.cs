using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using nem.Contracts.ControlPlane;
using nem.Contracts.Sentinel;
using Wolverine;

namespace nem.Mimir.Infrastructure.Health;

internal sealed class MimirHealthReportEmitter : BackgroundService
{
    private const string ServiceName = "nem.Mimir";
    private const string IntervalKey = "Sentinel:HealthReportIntervalSeconds";
    private const string MemoryLimitKey = "Sentinel:MemoryLimitMb";

    private readonly IServiceProvider _serviceProvider;
    private readonly IMessageBus _messageBus;
    private readonly ILogger<MimirHealthReportEmitter> _logger;
    private readonly TimeSpan _defaultInterval = TimeSpan.FromSeconds(30);
    private readonly double _defaultMemoryLimitMb = 2048;
    private readonly Stopwatch _cpuStopwatch = Stopwatch.StartNew();

    private TimeSpan _lastCpuTime = Process.GetCurrentProcess().TotalProcessorTime;

    public MimirHealthReportEmitter(
        IServiceProvider serviceProvider,
        IMessageBus messageBus,
        ILogger<MimirHealthReportEmitter> logger)
    {
        _serviceProvider = serviceProvider;
        _messageBus = messageBus;
        _logger = logger;
    }

    public async Task<TimeSpan> GetHealthReportIntervalAsync(CancellationToken cancellationToken = default)
    {
        var configManager = _serviceProvider.GetService<IConfigurationManager>();
        var value = configManager is null
            ? null
            : await configManager.GetConfigAsync(ServiceName, IntervalKey, cancellationToken);
        return int.TryParse(value, out var seconds) && seconds > 0
            ? TimeSpan.FromSeconds(seconds)
            : _defaultInterval;
    }

    public async Task<double> GetMemoryLimitMbAsync(CancellationToken cancellationToken = default)
    {
        var configManager = _serviceProvider.GetService<IConfigurationManager>();
        var value = configManager is null
            ? null
            : await configManager.GetConfigAsync(ServiceName, MemoryLimitKey, cancellationToken);
        return double.TryParse(value, out var limit) && limit > 0 ? limit : _defaultMemoryLimitMb;
    }

    public async Task PublishHealthReportOnceAsync(CancellationToken cancellationToken = default)
    {
        var memoryLimitMb = await GetMemoryLimitMbAsync(cancellationToken);
        var snapshot = GetSnapshot();
        var report = BuildHealthReport(
            snapshot.ProcessedCount,
            snapshot.FailedCount,
            snapshot.MemoryUsageMb,
            snapshot.CpuPercent,
            snapshot.ActiveConnections,
            snapshot.QueueDepth,
            memoryLimitMb);

        try
        {
            await _messageBus.PublishAsync(report);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish Mimir health report");
        }
    }

    public ServiceHealthReport BuildHealthReport(
        long processedCount,
        long failedCount,
        double memoryUsageMb,
        double cpuPercent,
        int activeConnections,
        int queueDepth,
        double memoryLimitMb)
    {
        var errorRate = CalculateErrorRate(processedCount, failedCount);
        var status = DetermineStatus(processedCount, failedCount, memoryUsageMb, memoryLimitMb);

        return new ServiceHealthReport
        {
            ServiceName = ServiceName,
            Timestamp = DateTimeOffset.UtcNow,
            Status = status,
            Metrics = new Dictionary<string, double>
            {
                ["processed_messages"] = processedCount,
                ["failed_messages"] = failedCount,
                ["error_rate"] = errorRate,
                ["memory_limit_mb"] = memoryLimitMb,
            },
            ActiveConnections = activeConnections,
            QueueDepth = queueDepth,
            MemoryUsageMb = memoryUsageMb,
            CpuPercent = cpuPercent,
            ErrorRate = errorRate,
            CustomDiagnostics = new Dictionary<string, string>
            {
                ["service"] = ServiceName,
                ["source"] = "nem.Mimir.Infrastructure",
                ["memory_limit_mb"] = memoryLimitMb.ToString(),
            },
        };
    }

    public static ServiceHealthStatus DetermineStatus(
        long processedCount,
        long failedCount,
        double memoryUsageMb,
        double memoryLimitMb)
    {
        var errorRate = CalculateErrorRate(processedCount, failedCount);
        var memoryRatio = memoryLimitMb <= 0 ? 0 : memoryUsageMb / memoryLimitMb;

        if (errorRate > 20 || memoryRatio > 0.9)
        {
            return ServiceHealthStatus.Unhealthy;
        }

        if (errorRate >= 5 || memoryRatio >= 0.8)
        {
            return ServiceHealthStatus.Degraded;
        }

        return ServiceHealthStatus.Healthy;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = await GetHealthReportIntervalAsync(stoppingToken);
        using var timer = new PeriodicTimer(interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await PublishHealthReportOnceAsync(stoppingToken);
        }
    }

    private HealthSnapshot GetSnapshot()
    {
        var process = Process.GetCurrentProcess();
        var cpuTime = process.TotalProcessorTime;
        var elapsed = _cpuStopwatch.Elapsed.TotalMilliseconds;
        var cpuPercent = elapsed <= 0
            ? 0
            : Math.Max(0, ((cpuTime - _lastCpuTime).TotalMilliseconds / elapsed) * 100 / Environment.ProcessorCount);

        _lastCpuTime = cpuTime;
        _cpuStopwatch.Restart();

        return new HealthSnapshot(
            ProcessedCount: 0,
            FailedCount: 0,
            MemoryUsageMb: GC.GetTotalMemory(false) / 1024d / 1024d,
            CpuPercent: cpuPercent,
            ActiveConnections: 0,
            QueueDepth: 0);
    }

    private static double CalculateErrorRate(long processedCount, long failedCount)
    {
        var total = processedCount + failedCount;
        return total <= 0 ? 0 : (double)failedCount / total * 100;
    }

    private sealed record HealthSnapshot(
        long ProcessedCount,
        long FailedCount,
        double MemoryUsageMb,
        double CpuPercent,
        int ActiveConnections,
        int QueueDepth);
}
