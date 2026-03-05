using System.Text.Json;
using Mimir.Application.Common.Interfaces;
using Mimir.Domain.Plugins;
using Mimir.Infrastructure.Tools;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;

namespace Mimir.Infrastructure.Tests.Tools;

public sealed class PluginToolProviderTests
{
    private readonly IPluginService _pluginService = Substitute.For<IPluginService>();
    private readonly PluginToolProvider _sut;

    public PluginToolProviderTests()
    {
        _sut = new PluginToolProvider(_pluginService);
    }

    [Fact]
    public async Task GetAvailableToolsAsync_MapsPluginsToToolDefinitions()
    {
        var plugins = new List<PluginMetadata>
        {
            PluginMetadata.Create("web-search", "Web Search", "1.0.0", "Searches the web"),
            PluginMetadata.Create("code-runner", "Code Runner", "1.0.0", "Runs code"),
        };
        _pluginService.ListPluginsAsync(Arg.Any<CancellationToken>())
            .Returns(plugins.AsReadOnly());

        var tools = await _sut.GetAvailableToolsAsync();

        tools.Count.ShouldBe(2);
        tools[0].Name.ShouldBe("web-search");
        tools[0].Description.ShouldBe("Searches the web");
        tools[0].ParametersJsonSchema.ShouldBeNull();
        tools[1].Name.ShouldBe("code-runner");
        tools[1].Description.ShouldBe("Runs code");
    }

    [Fact]
    public async Task GetAvailableToolsAsync_EmptyPluginList_ReturnsEmptyList()
    {
        _pluginService.ListPluginsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<PluginMetadata>().AsReadOnly());

        var tools = await _sut.GetAvailableToolsAsync();

        tools.ShouldBeEmpty();
    }

    [Fact]
    public async Task ExecuteToolAsync_RoutesToCorrectPluginWithParameters()
    {
        var expectedData = new Dictionary<string, object> { ["result"] = "found it" };
        var pluginResult = PluginResult.Success(expectedData);

        _pluginService.ExecutePluginAsync("web-search", Arg.Any<PluginContext>(), Arg.Any<CancellationToken>())
            .Returns(pluginResult);

        var args = JsonSerializer.Serialize(new Dictionary<string, string> { ["query"] = "test" });
        var result = await _sut.ExecuteToolAsync("web-search", args);

        result.IsSuccess.ShouldBeTrue();
        result.Content.ShouldNotBeNullOrWhiteSpace();
        result.ErrorMessage.ShouldBeNull();

        await _pluginService.Received(1)
            .ExecutePluginAsync("web-search", Arg.Is<PluginContext>(ctx =>
                ctx.Parameters.ContainsKey("query")), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteToolAsync_PluginFailure_ReturnToolResultFailure()
    {
        var pluginResult = PluginResult.Failure("Something went wrong");
        _pluginService.ExecutePluginAsync("bad-plugin", Arg.Any<PluginContext>(), Arg.Any<CancellationToken>())
            .Returns(pluginResult);

        var result = await _sut.ExecuteToolAsync("bad-plugin", "{}");

        result.IsSuccess.ShouldBeFalse();
        result.ErrorMessage.ShouldBe("Something went wrong");
    }

    [Fact]
    public async Task ExecuteToolAsync_PluginNotFound_ReturnsFailure()
    {
        _pluginService.ExecutePluginAsync("missing", Arg.Any<PluginContext>(), Arg.Any<CancellationToken>())
            .Throws(new KeyNotFoundException("Plugin 'missing' is not loaded."));

        var result = await _sut.ExecuteToolAsync("missing", "{}");

        result.IsSuccess.ShouldBeFalse();
        result.ErrorMessage!.ShouldContain("not found");
    }

    [Fact]
    public async Task ExecuteToolAsync_EmptyArgumentsJson_HandlesGracefully()
    {
        var pluginResult = PluginResult.Success();
        _pluginService.ExecutePluginAsync("test", Arg.Any<PluginContext>(), Arg.Any<CancellationToken>())
            .Returns(pluginResult);

        var result = await _sut.ExecuteToolAsync("test", "");

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task ExecuteToolAsync_InvalidJson_HandlesGracefully()
    {
        var pluginResult = PluginResult.Success();
        _pluginService.ExecutePluginAsync("test", Arg.Any<PluginContext>(), Arg.Any<CancellationToken>())
            .Returns(pluginResult);

        var result = await _sut.ExecuteToolAsync("test", "not-json{{{");

        result.IsSuccess.ShouldBeTrue();
    }
}
