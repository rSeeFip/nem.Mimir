using nem.Mimir.Domain.Plugins;
using nem.Mimir.Infrastructure.Plugins.BuiltIn;
using Shouldly;

namespace nem.Mimir.Infrastructure.Tests.Plugins.BuiltIn;

public sealed class WebSearchPluginTests
{
    private readonly WebSearchPlugin _sut = new();

    [Fact]
    public void Metadata_ShouldHaveCorrectValues()
    {
        _sut.Id.ShouldBe("mimir.builtin.web-search");
        _sut.Name.ShouldBe("Web Search");
        _sut.Version.ShouldBe("1.0.0");
        _sut.Description.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ExecuteAsync_WithValidQuery_ReturnsNotConfigured()
    {
        var context = PluginContext.Create("user-1", new Dictionary<string, object>
        {
            ["query"] = "latest AI news",
        });

        var result = await _sut.ExecuteAsync(context);

        result.IsSuccess.ShouldBeFalse();
        result.ErrorMessage!.ShouldContain("not yet configured");
        result.ErrorMessage!.ShouldContain("search provider");
    }

    [Fact]
    public async Task ExecuteAsync_WithMissingQueryParam_ReturnsFailure()
    {
        var context = PluginContext.Create("user-1", new Dictionary<string, object>());

        var result = await _sut.ExecuteAsync(context);

        result.IsSuccess.ShouldBeFalse();
        result.ErrorMessage!.ShouldContain("query");
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyQueryParam_ReturnsFailure()
    {
        var context = PluginContext.Create("user-1", new Dictionary<string, object>
        {
            ["query"] = "  ",
        });

        var result = await _sut.ExecuteAsync(context);

        result.IsSuccess.ShouldBeFalse();
        result.ErrorMessage!.ShouldContain("query");
    }

    [Fact]
    public async Task ExecuteAsync_WithNonStringQueryParam_ReturnsFailure()
    {
        var context = PluginContext.Create("user-1", new Dictionary<string, object>
        {
            ["query"] = 123,
        });

        var result = await _sut.ExecuteAsync(context);

        result.IsSuccess.ShouldBeFalse();
        result.ErrorMessage!.ShouldContain("query");
    }

    [Fact]
    public async Task InitializeAsync_CompletesSuccessfully()
    {
        await _sut.InitializeAsync();
        // Should not throw
    }

    [Fact]
    public async Task ShutdownAsync_CompletesSuccessfully()
    {
        await _sut.ShutdownAsync();
        // Should not throw
    }
}
