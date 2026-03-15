namespace nem.Mimir.Infrastructure.Tests.McpServers;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using nem.Mimir.Domain.McpServers;
using nem.Mimir.Infrastructure.McpServers;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;

public class McpClientManagerTests
{
    private readonly ILoggerFactory _loggerFactory = NullLoggerFactory.Instance;

    private McpClientManager CreateSut() => new(_loggerFactory);

    [Fact]
    public async Task GetConnectedServersAsync_returns_empty_when_no_connections()
    {
        var sut = CreateSut();

        var result = await sut.GetConnectedServersAsync();

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task DisconnectAsync_does_not_throw_for_unknown_server()
    {
        var sut = CreateSut();

        await Should.NotThrowAsync(() => sut.DisconnectAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task HealthCheckAsync_returns_false_for_unknown_server()
    {
        var sut = CreateSut();

        var result = await sut.HealthCheckAsync(Guid.NewGuid());

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task GetServerToolsAsync_throws_for_unknown_server()
    {
        var sut = CreateSut();

        await Should.ThrowAsync<InvalidOperationException>(
            () => sut.GetServerToolsAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task ExecuteToolAsync_throws_for_unknown_server()
    {
        var sut = CreateSut();

        await Should.ThrowAsync<InvalidOperationException>(
            () => sut.ExecuteToolAsync(Guid.NewGuid(), "test-tool", null));
    }

    [Fact]
    public async Task ConnectAsync_throws_after_max_retries_for_invalid_stdio_config()
    {
        var sut = CreateSut();
        var config = new McpServerConfig
        {
            Id = Guid.NewGuid(),
            Name = "Bad Server",
            TransportType = McpTransportType.Stdio,
            Command = "nonexistent-binary-that-does-not-exist-12345",
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        await Should.ThrowAsync<Exception>(
            () => sut.ConnectAsync(config, CancellationToken.None));

        var servers = await sut.GetConnectedServersAsync();
        servers.ShouldBeEmpty();
    }

    [Fact]
    public async Task ConnectAsync_throws_for_sse_config_without_url()
    {
        var sut = CreateSut();
        var config = new McpServerConfig
        {
            Id = Guid.NewGuid(),
            Name = "No URL Server",
            TransportType = McpTransportType.Sse,
            Url = null,
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        await Should.ThrowAsync<InvalidOperationException>(
            () => sut.ConnectAsync(config, CancellationToken.None));
    }

    [Fact]
    public async Task ConnectAsync_throws_for_stdio_config_without_command()
    {
        var sut = CreateSut();
        var config = new McpServerConfig
        {
            Id = Guid.NewGuid(),
            Name = "No Command Server",
            TransportType = McpTransportType.Stdio,
            Command = null,
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        await Should.ThrowAsync<InvalidOperationException>(
            () => sut.ConnectAsync(config, CancellationToken.None));
    }

    [Fact]
    public async Task DisposeAsync_completes_without_error_when_empty()
    {
        var sut = CreateSut();

        await Should.NotThrowAsync(async () => await sut.DisposeAsync());
    }
}

public class McpClientStartupServiceTests
{
    [Fact]
    public async Task StartAsync_connects_enabled_servers()
    {
        var clientManager = Substitute.For<IMcpClientManager>();
        var repo = Substitute.For<IMcpServerConfigRepository>();
        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        var scope = Substitute.For<IServiceScope>();
        var serviceProvider = Substitute.For<IServiceProvider>();

        var enabledConfigs = new List<McpServerConfig>
        {
            new() { Id = Guid.NewGuid(), Name = "Server A", TransportType = McpTransportType.Stdio, Command = "cmd", IsEnabled = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), Name = "Server B", TransportType = McpTransportType.Sse, Url = "http://localhost", IsEnabled = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
        };

        scopeFactory.CreateScope().Returns(scope);
        scope.ServiceProvider.Returns(serviceProvider);
        serviceProvider.GetService(typeof(IMcpServerConfigRepository)).Returns(repo);
        repo.GetEnabledAsync(Arg.Any<CancellationToken>()).Returns(enabledConfigs);

        var sut = new McpClientStartupService(clientManager, scopeFactory, NullLogger<McpClientStartupService>.Instance);

        await sut.StartAsync(CancellationToken.None);

        await clientManager.Received(2).ConnectAsync(Arg.Any<McpServerConfig>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartAsync_continues_when_one_server_fails()
    {
        var clientManager = Substitute.For<IMcpClientManager>();
        var repo = Substitute.For<IMcpServerConfigRepository>();
        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        var scope = Substitute.For<IServiceScope>();
        var serviceProvider = Substitute.For<IServiceProvider>();

        var configs = new List<McpServerConfig>
        {
            new() { Id = Guid.NewGuid(), Name = "Failing Server", TransportType = McpTransportType.Stdio, Command = "bad", IsEnabled = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), Name = "Good Server", TransportType = McpTransportType.Stdio, Command = "good", IsEnabled = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
        };

        scopeFactory.CreateScope().Returns(scope);
        scope.ServiceProvider.Returns(serviceProvider);
        serviceProvider.GetService(typeof(IMcpServerConfigRepository)).Returns(repo);
        repo.GetEnabledAsync(Arg.Any<CancellationToken>()).Returns(configs);

        clientManager.ConnectAsync(configs[0], Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("Connection failed"));

        var sut = new McpClientStartupService(clientManager, scopeFactory, NullLogger<McpClientStartupService>.Instance);

        await Should.NotThrowAsync(() => sut.StartAsync(CancellationToken.None));

        await clientManager.Received(1).ConnectAsync(configs[1], Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StopAsync_disconnects_all_connected_servers()
    {
        var clientManager = Substitute.For<IMcpClientManager>();
        var scopeFactory = Substitute.For<IServiceScopeFactory>();

        var connectedServers = new List<McpServerConfig>
        {
            new() { Id = Guid.NewGuid(), Name = "Server 1", TransportType = McpTransportType.Stdio, Command = "cmd", IsEnabled = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), Name = "Server 2", TransportType = McpTransportType.Sse, Url = "http://localhost", IsEnabled = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
        };

        clientManager.GetConnectedServersAsync(Arg.Any<CancellationToken>()).Returns(connectedServers);

        var sut = new McpClientStartupService(clientManager, scopeFactory, NullLogger<McpClientStartupService>.Instance);

        await sut.StopAsync(CancellationToken.None);

        await clientManager.Received(1).DisconnectAsync(connectedServers[0].Id, Arg.Any<CancellationToken>());
        await clientManager.Received(1).DisconnectAsync(connectedServers[1].Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartAsync_does_nothing_when_no_enabled_servers()
    {
        var clientManager = Substitute.For<IMcpClientManager>();
        var repo = Substitute.For<IMcpServerConfigRepository>();
        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        var scope = Substitute.For<IServiceScope>();
        var serviceProvider = Substitute.For<IServiceProvider>();

        scopeFactory.CreateScope().Returns(scope);
        scope.ServiceProvider.Returns(serviceProvider);
        serviceProvider.GetService(typeof(IMcpServerConfigRepository)).Returns(repo);
        repo.GetEnabledAsync(Arg.Any<CancellationToken>()).Returns(new List<McpServerConfig>());

        var sut = new McpClientStartupService(clientManager, scopeFactory, NullLogger<McpClientStartupService>.Instance);

        await sut.StartAsync(CancellationToken.None);

        await clientManager.DidNotReceive().ConnectAsync(Arg.Any<McpServerConfig>(), Arg.Any<CancellationToken>());
    }
}
