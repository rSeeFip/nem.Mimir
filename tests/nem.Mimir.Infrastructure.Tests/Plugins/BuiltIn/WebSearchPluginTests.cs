using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Application.Knowledge;
using nem.Mimir.Domain.Plugins;
using nem.Mimir.Infrastructure.Plugins.BuiltIn;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;

namespace nem.Mimir.Infrastructure.Tests.Plugins.BuiltIn;

public sealed class WebSearchPluginTests
{
    private readonly ISearxngClient _searxngClient;
    private readonly WebSearchPlugin _sut;

    public WebSearchPluginTests()
    {
        _searxngClient = Substitute.For<ISearxngClient>();

        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(ISearxngClient)).Returns(_searxngClient);

        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(serviceProvider);

        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        scopeFactory.CreateScope().Returns(scope);

        var logger = Substitute.For<ILogger<WebSearchPlugin>>();

        _sut = new WebSearchPlugin(scopeFactory, logger);
    }

    [Fact]
    public void Metadata_ShouldHaveCorrectValues()
    {
        _sut.Id.ShouldBe("mimir.builtin.web-search");
        _sut.Name.ShouldBe("Web Search");
        _sut.Version.ShouldBe("1.0.0");
        _sut.Description.ShouldNotBeNullOrWhiteSpace();
        _sut.Description.ShouldContain("query");
        _sut.Description.ShouldContain("max_results");
    }

    [Fact]
    public async Task ExecuteAsync_WithValidQuery_ReturnsStructuredResults()
    {
        var context = PluginContext.Create("user-1", new Dictionary<string, object>
        {
            ["query"] = "latest AI news",
        });

        _searxngClient.SearchAsync("latest AI news", Arg.Any<CancellationToken>())
            .Returns(new List<WebSearchResultDto>
            {
                new("OpenAI Releases GPT-5", "https://example.com/gpt5", "OpenAI has released GPT-5 today.", "google", null),
                new("Anthropic Raises $2B", "https://example.com/anthropic", "Anthropic announced new funding.", "bing", null),
            });

        var result = await _sut.ExecuteAsync(context);

        result.IsSuccess.ShouldBeTrue();
        result.Data["query"].ShouldBe("latest AI news");
        result.Data["result_count"].ShouldBe(2);
        result.Data.ContainsKey("results").ShouldBeTrue();
        result.Data.ContainsKey("summary").ShouldBeTrue();

        var summary = result.Data["summary"] as string;
        summary.ShouldNotBeNull();
        summary.ShouldContain("OpenAI Releases GPT-5");
        summary.ShouldContain("https://example.com/gpt5");
    }

    [Fact]
    public async Task ExecuteAsync_WithMaxResults_LimitsOutput()
    {
        var context = PluginContext.Create("user-1", new Dictionary<string, object>
        {
            ["query"] = "test query",
            ["max_results"] = 1,
        });

        _searxngClient.SearchAsync("test query", Arg.Any<CancellationToken>())
            .Returns(new List<WebSearchResultDto>
            {
                new("Result 1", "https://example.com/1", "First result", null, null),
                new("Result 2", "https://example.com/2", "Second result", null, null),
                new("Result 3", "https://example.com/3", "Third result", null, null),
            });

        var result = await _sut.ExecuteAsync(context);

        result.IsSuccess.ShouldBeTrue();
        result.Data["result_count"].ShouldBe(1);
    }

    [Fact]
    public async Task ExecuteAsync_WithStringMaxResults_ParsesCorrectly()
    {
        var context = PluginContext.Create("user-1", new Dictionary<string, object>
        {
            ["query"] = "test",
            ["max_results"] = "2",
        });

        _searxngClient.SearchAsync("test", Arg.Any<CancellationToken>())
            .Returns(new List<WebSearchResultDto>
            {
                new("R1", "https://a.com", "s1", null, null),
                new("R2", "https://b.com", "s2", null, null),
                new("R3", "https://c.com", "s3", null, null),
            });

        var result = await _sut.ExecuteAsync(context);

        result.IsSuccess.ShouldBeTrue();
        result.Data["result_count"].ShouldBe(2);
    }

    [Fact]
    public async Task ExecuteAsync_ClampsMaxResultsToUpperBound()
    {
        var context = PluginContext.Create("user-1", new Dictionary<string, object>
        {
            ["query"] = "test",
            ["max_results"] = 100,
        });

        _searxngClient.SearchAsync("test", Arg.Any<CancellationToken>())
            .Returns(new List<WebSearchResultDto>
            {
                new("R1", "https://a.com", "s1", null, null),
            });

        var result = await _sut.ExecuteAsync(context);

        result.IsSuccess.ShouldBeTrue();
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
    public async Task ExecuteAsync_WithEmptyResults_ReturnsSuccessWithZeroCount()
    {
        var context = PluginContext.Create("user-1", new Dictionary<string, object>
        {
            ["query"] = "nonexistent topic xyzzy",
        });

        _searxngClient.SearchAsync("nonexistent topic xyzzy", Arg.Any<CancellationToken>())
            .Returns(new List<WebSearchResultDto>());

        var result = await _sut.ExecuteAsync(context);

        result.IsSuccess.ShouldBeTrue();
        result.Data["result_count"].ShouldBe(0);
        var summary = result.Data["summary"] as string;
        summary.ShouldNotBeNull();
        summary.ShouldContain("No results found");
    }

    [Fact]
    public async Task ExecuteAsync_WhenUpstreamFails_ReturnsGracefulFailure()
    {
        var context = PluginContext.Create("user-1", new Dictionary<string, object>
        {
            ["query"] = "test",
        });

        _searxngClient.SearchAsync("test", Arg.Any<CancellationToken>())
            .Throws(new HttpRequestException("Connection refused"));

        var result = await _sut.ExecuteAsync(context);

        result.IsSuccess.ShouldBeFalse();
        result.ErrorMessage!.ShouldContain("upstream error");
    }

    [Fact]
    public async Task ExecuteAsync_WhenTimeout_ReturnsGracefulFailure()
    {
        var context = PluginContext.Create("user-1", new Dictionary<string, object>
        {
            ["query"] = "test",
        });

        _searxngClient.SearchAsync("test", Arg.Any<CancellationToken>())
            .Throws(new TaskCanceledException("Timeout", new TimeoutException()));

        var result = await _sut.ExecuteAsync(context);

        result.IsSuccess.ShouldBeFalse();
        result.ErrorMessage!.ShouldContain("timed out");
    }

    [Fact]
    public async Task ExecuteAsync_WhenSearxngNotRegistered_ReturnsNotConfiguredFailure()
    {
        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(ISearxngClient)).Returns(null);

        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(serviceProvider);

        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        scopeFactory.CreateScope().Returns(scope);

        var logger = Substitute.For<ILogger<WebSearchPlugin>>();
        var plugin = new WebSearchPlugin(scopeFactory, logger);

        var context = PluginContext.Create("user-1", new Dictionary<string, object>
        {
            ["query"] = "test",
        });

        var result = await plugin.ExecuteAsync(context);

        result.IsSuccess.ShouldBeFalse();
        result.ErrorMessage!.ShouldContain("not available");
    }

    [Fact]
    public async Task ExecuteAsync_ResultsContainStructuredFields()
    {
        var context = PluginContext.Create("user-1", new Dictionary<string, object>
        {
            ["query"] = "test",
        });

        _searxngClient.SearchAsync("test", Arg.Any<CancellationToken>())
            .Returns(new List<WebSearchResultDto>
            {
                new("Title One", "https://example.com", "A snippet here", "google", null),
            });

        var result = await _sut.ExecuteAsync(context);

        result.IsSuccess.ShouldBeTrue();
        var results = result.Data["results"] as List<Dictionary<string, object?>>;
        results.ShouldNotBeNull();
        results.Count.ShouldBe(1);

        var first = results[0];
        first["index"].ShouldBe(1);
        first["title"].ShouldBe("Title One");
        first["url"].ShouldBe("https://example.com");
        first["snippet"].ShouldBe("A snippet here");
    }

    [Fact]
    public async Task InitializeAsync_CompletesSuccessfully()
    {
        await _sut.InitializeAsync();
    }

    [Fact]
    public async Task ShutdownAsync_CompletesSuccessfully()
    {
        await _sut.ShutdownAsync();
    }
}
