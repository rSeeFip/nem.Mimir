using Microsoft.Extensions.Logging;
using Mimir.Domain.McpServers;
using Mimir.Domain.Tools;
using Mimir.Infrastructure.Tools;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;

namespace Mimir.Infrastructure.Tests.Tools;

public sealed class McpToolProviderTests
{
    private readonly IMcpClientManager _clientManager = Substitute.For<IMcpClientManager>();
    private readonly ILogger<McpToolProvider> _logger = Substitute.For<ILogger<McpToolProvider>>();
    private readonly McpToolProvider _sut;

    public McpToolProviderTests()
    {
        _sut = new McpToolProvider(_clientManager, _logger);
    }

    [Fact]
    public async Task GetAvailableToolsAsync_NoConnectedServers_ReturnsEmpty()
    {
        _clientManager.GetConnectedServersAsync(Arg.Any<CancellationToken>())
            .Returns(Array.Empty<McpServerConfig>());

        var tools = await _sut.GetAvailableToolsAsync();

        tools.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetAvailableToolsAsync_SingleServer_ReturnsTools()
    {
        var server = CreateServer("server-a");
        var serverTools = new List<ToolDefinition>
        {
            new("search", "Search the web"),
            new("fetch", "Fetch a URL", """{"type":"object"}"""),
        };

        _clientManager.GetConnectedServersAsync(Arg.Any<CancellationToken>())
            .Returns(new[] { server });
        _clientManager.GetServerToolsAsync(server.Id, Arg.Any<CancellationToken>())
            .Returns(serverTools.AsReadOnly());

        var tools = await _sut.GetAvailableToolsAsync();

        tools.Count.ShouldBe(2);
        tools[0].Name.ShouldBe("search");
        tools[0].Description.ShouldBe("Search the web");
        tools[1].Name.ShouldBe("fetch");
        tools[1].ParametersJsonSchema.ShouldBe("""{"type":"object"}""");
    }

    [Fact]
    public async Task GetAvailableToolsAsync_MultipleServers_AggregatesTools()
    {
        var serverA = CreateServer("alpha");
        var serverB = CreateServer("beta");

        _clientManager.GetConnectedServersAsync(Arg.Any<CancellationToken>())
            .Returns(new[] { serverA, serverB });
        _clientManager.GetServerToolsAsync(serverA.Id, Arg.Any<CancellationToken>())
            .Returns(new[] { new ToolDefinition("tool-a", "From alpha") });
        _clientManager.GetServerToolsAsync(serverB.Id, Arg.Any<CancellationToken>())
            .Returns(new[] { new ToolDefinition("tool-b", "From beta") });

        var tools = await _sut.GetAvailableToolsAsync();

        tools.Count.ShouldBe(2);
        tools.ShouldContain(t => t.Name == "tool-a");
        tools.ShouldContain(t => t.Name == "tool-b");
    }

    [Fact]
    public async Task GetAvailableToolsAsync_NameCollision_NamespacesWithServerName()
    {
        var serverA = CreateServer("alpha");
        var serverB = CreateServer("beta");

        _clientManager.GetConnectedServersAsync(Arg.Any<CancellationToken>())
            .Returns(new[] { serverA, serverB });
        _clientManager.GetServerToolsAsync(serverA.Id, Arg.Any<CancellationToken>())
            .Returns(new[] { new ToolDefinition("search", "Alpha search") });
        _clientManager.GetServerToolsAsync(serverB.Id, Arg.Any<CancellationToken>())
            .Returns(new[] { new ToolDefinition("search", "Beta search") });

        var tools = await _sut.GetAvailableToolsAsync();

        tools.Count.ShouldBe(2);
        tools.ShouldContain(t => t.Name == "alpha_search" && t.Description == "Alpha search");
        tools.ShouldContain(t => t.Name == "beta_search" && t.Description == "Beta search");
    }

    [Fact]
    public async Task ExecuteToolAsync_RoutesToCorrectServer()
    {
        var serverA = CreateServer("alpha");
        var serverB = CreateServer("beta");
        var expectedResult = ToolResult.Success("result from beta");

        _clientManager.GetConnectedServersAsync(Arg.Any<CancellationToken>())
            .Returns(new[] { serverA, serverB });
        _clientManager.GetServerToolsAsync(serverA.Id, Arg.Any<CancellationToken>())
            .Returns(new[] { new ToolDefinition("tool-a", "Alpha tool") });
        _clientManager.GetServerToolsAsync(serverB.Id, Arg.Any<CancellationToken>())
            .Returns(new[] { new ToolDefinition("tool-b", "Beta tool") });
        _clientManager.ExecuteToolAsync(serverB.Id, "tool-b", """{"q":"test"}""", Arg.Any<CancellationToken>())
            .Returns(expectedResult);

        // Populate the cache
        await _sut.GetAvailableToolsAsync();

        var result = await _sut.ExecuteToolAsync("tool-b", """{"q":"test"}""");

        result.IsSuccess.ShouldBeTrue();
        result.Content.ShouldBe("result from beta");
        await _clientManager.Received(1).ExecuteToolAsync(serverB.Id, "tool-b", """{"q":"test"}""", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteToolAsync_NamespacedTool_RoutesToCorrectServer()
    {
        var serverA = CreateServer("alpha");
        var serverB = CreateServer("beta");
        var expectedResult = ToolResult.Success("namespaced result");

        _clientManager.GetConnectedServersAsync(Arg.Any<CancellationToken>())
            .Returns(new[] { serverA, serverB });
        _clientManager.GetServerToolsAsync(serverA.Id, Arg.Any<CancellationToken>())
            .Returns(new[] { new ToolDefinition("search", "Alpha search") });
        _clientManager.GetServerToolsAsync(serverB.Id, Arg.Any<CancellationToken>())
            .Returns(new[] { new ToolDefinition("search", "Beta search") });
        _clientManager.ExecuteToolAsync(serverB.Id, "search", "{}", Arg.Any<CancellationToken>())
            .Returns(expectedResult);

        // Populate the cache (will namespace to alpha_search, beta_search)
        await _sut.GetAvailableToolsAsync();

        var result = await _sut.ExecuteToolAsync("beta_search", "{}");

        result.IsSuccess.ShouldBeTrue();
        result.Content.ShouldBe("namespaced result");
    }

    [Fact]
    public async Task ExecuteToolAsync_ToolNotFound_ReturnsFailure()
    {
        _clientManager.GetConnectedServersAsync(Arg.Any<CancellationToken>())
            .Returns(Array.Empty<McpServerConfig>());

        var result = await _sut.ExecuteToolAsync("nonexistent", "{}");

        result.IsSuccess.ShouldBeFalse();
        result.ErrorMessage.ShouldNotBeNull();
        result.ErrorMessage!.ShouldContain("nonexistent");
        result.ErrorMessage!.ShouldContain("not found");
    }

    [Fact]
    public async Task GetAvailableToolsAsync_ServerError_SkipsAndContinues()
    {
        var healthyServer = CreateServer("healthy");
        var brokenServer = CreateServer("broken");

        _clientManager.GetConnectedServersAsync(Arg.Any<CancellationToken>())
            .Returns(new[] { brokenServer, healthyServer });
        _clientManager.GetServerToolsAsync(brokenServer.Id, Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("Connection lost"));
        _clientManager.GetServerToolsAsync(healthyServer.Id, Arg.Any<CancellationToken>())
            .Returns(new[] { new ToolDefinition("working-tool", "Works fine") });

        var tools = await _sut.GetAvailableToolsAsync();

        tools.Count.ShouldBe(1);
        tools[0].Name.ShouldBe("working-tool");
    }

    private static McpServerConfig CreateServer(string name) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        TransportType = McpTransportType.Stdio,
        IsEnabled = true,
    };
}
