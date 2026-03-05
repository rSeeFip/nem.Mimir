namespace Mimir.Infrastructure.Tests.McpServers;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Mimir.Domain.McpServers;
using Mimir.Infrastructure.McpServers;
using NSubstitute;
using Shouldly;

public class McpConfigChangeListenerTests
{
    private readonly IMcpClientManager _clientManager = Substitute.For<IMcpClientManager>();
    private readonly IMcpServerConfigRepository _repo = Substitute.For<IMcpServerConfigRepository>();
    private readonly IServiceScopeFactory _scopeFactory;

    public McpConfigChangeListenerTests()
    {
        var scope = Substitute.For<IServiceScope>();
        var serviceProvider = Substitute.For<IServiceProvider>();
        _scopeFactory = Substitute.For<IServiceScopeFactory>();

        _scopeFactory.CreateScope().Returns(scope);
        scope.ServiceProvider.Returns(serviceProvider);
        serviceProvider.GetService(typeof(IMcpServerConfigRepository)).Returns(_repo);
    }

    private McpConfigChangeListener CreateSut() =>
        new(_scopeFactory, _clientManager, NullLogger<McpConfigChangeListener>.Instance);

    private static McpServerConfig CreateConfig(
        Guid? id = null,
        string name = "Test Server",
        bool isEnabled = true,
        McpTransportType transportType = McpTransportType.Stdio,
        string? command = "test-cmd",
        DateTime? updatedAt = null)
    {
        return new McpServerConfig
        {
            Id = id ?? Guid.NewGuid(),
            Name = name,
            TransportType = transportType,
            Command = command,
            IsEnabled = isEnabled,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = updatedAt ?? DateTime.UtcNow,
        };
    }

    [Fact]
    public async Task CheckForChanges_DetectsNewEnabledConfig_Connects()
    {
        var sut = CreateSut();

        _repo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<McpServerConfig>());

        await sut.CheckForChangesAsync(CancellationToken.None);

        var newConfig = CreateConfig(name: "New Server", isEnabled: true);
        _repo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<McpServerConfig> { newConfig });

        await sut.CheckForChangesAsync(CancellationToken.None);

        await _clientManager.Received(1)
            .ConnectAsync(Arg.Is<McpServerConfig>(c => c.Id == newConfig.Id), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckForChanges_DetectsDeletedConfig_Disconnects()
    {
        var sut = CreateSut();
        var existingConfig = CreateConfig(name: "Existing Server");

        _repo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<McpServerConfig> { existingConfig });

        await sut.CheckForChangesAsync(CancellationToken.None);

        _repo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<McpServerConfig>());

        await sut.CheckForChangesAsync(CancellationToken.None);

        await _clientManager.Received(1)
            .DisconnectAsync(existingConfig.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckForChanges_DetectsModifiedConfig_Reconnects()
    {
        var sut = CreateSut();
        var serverId = Guid.NewGuid();
        var originalConfig = CreateConfig(
            id: serverId,
            name: "Server",
            updatedAt: new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        _repo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<McpServerConfig> { originalConfig });

        await sut.CheckForChangesAsync(CancellationToken.None);
        _clientManager.ClearReceivedCalls();

        var modifiedConfig = CreateConfig(
            id: serverId,
            name: "Server",
            updatedAt: new DateTime(2025, 1, 2, 0, 0, 0, DateTimeKind.Utc));

        _repo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<McpServerConfig> { modifiedConfig });

        await sut.CheckForChangesAsync(CancellationToken.None);

        await _clientManager.Received(1)
            .DisconnectAsync(serverId, Arg.Any<CancellationToken>());
        await _clientManager.Received(1)
            .ConnectAsync(Arg.Is<McpServerConfig>(c => c.Id == serverId), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckForChanges_DisabledConfig_DoesNotConnect()
    {
        var sut = CreateSut();

        _repo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<McpServerConfig>());

        await sut.CheckForChangesAsync(CancellationToken.None);

        var disabledConfig = CreateConfig(name: "Disabled Server", isEnabled: false);
        _repo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<McpServerConfig> { disabledConfig });

        await sut.CheckForChangesAsync(CancellationToken.None);

        await _clientManager.DidNotReceive()
            .ConnectAsync(Arg.Any<McpServerConfig>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckForChanges_NoChanges_DoesNothing()
    {
        var sut = CreateSut();
        var config = CreateConfig(name: "Stable Server");

        _repo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<McpServerConfig> { config });

        await sut.CheckForChangesAsync(CancellationToken.None);

        _clientManager.ClearReceivedCalls();

        await sut.CheckForChangesAsync(CancellationToken.None);

        await _clientManager.DidNotReceive()
            .ConnectAsync(Arg.Any<McpServerConfig>(), Arg.Any<CancellationToken>());
        await _clientManager.DidNotReceive()
            .DisconnectAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TriggerRefreshAsync_ConcurrentCalls_SerializedBySemaphore()
    {
        var sut = CreateSut();
        var callOrder = new List<int>();
        var tcs = new TaskCompletionSource();

        _repo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<McpServerConfig>());

        await sut.CheckForChangesAsync(CancellationToken.None);

        var config = CreateConfig(name: "Concurrent Server");
        var callCount = 0;
        _repo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                var current = Interlocked.Increment(ref callCount);
                callOrder.Add(current);
                if (current == 1)
                {
                    await tcs.Task;
                }
                return (IReadOnlyList<McpServerConfig>)new List<McpServerConfig> { config };
            });

        var task1 = sut.TriggerRefreshAsync();
        await Task.Delay(50);
        var task2 = sut.TriggerRefreshAsync();

        await Task.Delay(50);
        callOrder.Count.ShouldBe(1);

        tcs.SetResult();
        await Task.WhenAll(task1, task2);

        callOrder.Count.ShouldBe(2);
    }

    [Fact]
    public async Task HasConfigChanged_DetectsEnabledChange()
    {
        var config1 = CreateConfig(isEnabled: true);
        var config2 = CreateConfig(isEnabled: false);
        config2.UpdatedAt = config1.UpdatedAt;
        config2.TransportType = config1.TransportType;
        config2.Command = config1.Command;
        config2.Arguments = config1.Arguments;
        config2.Url = config1.Url;
        config2.EnvironmentVariablesJson = config1.EnvironmentVariablesJson;

        McpConfigChangeListener.HasConfigChanged(config1, config2).ShouldBeTrue();
    }

    [Fact]
    public async Task HasConfigChanged_IdenticalConfigs_ReturnsFalse()
    {
        var now = DateTime.UtcNow;
        var config1 = CreateConfig(updatedAt: now);
        var config2 = CreateConfig(updatedAt: now);
        config2.TransportType = config1.TransportType;
        config2.Command = config1.Command;
        config2.Arguments = config1.Arguments;
        config2.Url = config1.Url;
        config2.EnvironmentVariablesJson = config1.EnvironmentVariablesJson;
        config2.IsEnabled = config1.IsEnabled;

        McpConfigChangeListener.HasConfigChanged(config1, config2).ShouldBeFalse();
    }
}
