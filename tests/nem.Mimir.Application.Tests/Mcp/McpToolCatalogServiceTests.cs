using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using nem.Mimir.Application.Mcp;
using nem.Mimir.Domain.Tools;
using NSubstitute;
using Shouldly;

namespace nem.Mimir.Application.Tests.Mcp;

public sealed class McpToolCatalogServiceTests
{
    private readonly IToolProvider _toolProvider = Substitute.For<IToolProvider>();
    private readonly IPersonaToolFilter _personaFilter = Substitute.For<IPersonaToolFilter>();
    private readonly IMemoryCache _cache = new MemoryCache(new MemoryCacheOptions());
    private readonly ILogger<McpToolCatalogService> _logger = Substitute.For<ILogger<McpToolCatalogService>>();

    #region Constructor Validation

    [Fact]
    public void Constructor_ThrowsOnNullToolProvider()
    {
        Should.Throw<ArgumentNullException>(() =>
            new McpToolCatalogService(null!, _personaFilter, _cache, new McpToolCatalogOptions(), _logger));
    }

    [Fact]
    public void Constructor_ThrowsOnNullPersonaFilter()
    {
        Should.Throw<ArgumentNullException>(() =>
            new McpToolCatalogService(_toolProvider, null!, _cache, new McpToolCatalogOptions(), _logger));
    }

    [Fact]
    public void Constructor_ThrowsOnNullCache()
    {
        Should.Throw<ArgumentNullException>(() =>
            new McpToolCatalogService(_toolProvider, _personaFilter, null!, new McpToolCatalogOptions(), _logger));
    }

    [Fact]
    public void Constructor_ThrowsOnNullOptions()
    {
        Should.Throw<ArgumentNullException>(() =>
            new McpToolCatalogService(_toolProvider, _personaFilter, _cache, null!, _logger));
    }

    [Fact]
    public void Constructor_ThrowsOnNullLogger()
    {
        Should.Throw<ArgumentNullException>(() =>
            new McpToolCatalogService(_toolProvider, _personaFilter, _cache, new McpToolCatalogOptions(), null!));
    }

    #endregion

    #region ListToolsAsync — Aggregation

    [Fact]
    public async Task ListToolsAsync_ReturnsAllToolsFromProvider()
    {
        var tools = CreateSampleTools();
        _toolProvider.ListToolsAsync(Arg.Any<CancellationToken>()).Returns(tools);
        var sut = CreateSut();

        var result = await sut.ListToolsAsync(cancellationToken: CancellationToken.None);

        result.Count.ShouldBe(3);
        result.ShouldContain(t => t.Name == "knowhub.search");
        result.ShouldContain(t => t.Name == "knowhub.ingest");
        result.ShouldContain(t => t.Name == "mediahub.upload");
    }

    [Fact]
    public async Task ListToolsAsync_ReturnsEmptyWhenNoToolsAvailable()
    {
        _toolProvider.ListToolsAsync(Arg.Any<CancellationToken>())
            .Returns(Array.Empty<ToolDefinition>());
        var sut = CreateSut();

        var result = await sut.ListToolsAsync(cancellationToken: CancellationToken.None);

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task ListToolsAsync_CancellationRequested_Throws()
    {
        var sut = CreateSut();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Should.ThrowAsync<OperationCanceledException>(
            () => sut.ListToolsAsync(cancellationToken: cts.Token));
    }

    #endregion

    #region ListToolsAsync — Caching

    [Fact]
    public async Task ListToolsAsync_CachesResults()
    {
        var tools = CreateSampleTools();
        _toolProvider.ListToolsAsync(Arg.Any<CancellationToken>()).Returns(tools);
        var sut = CreateSut();

        // First call — should hit provider
        var result1 = await sut.ListToolsAsync(cancellationToken: CancellationToken.None);
        // Second call — should hit cache
        var result2 = await sut.ListToolsAsync(cancellationToken: CancellationToken.None);

        result1.Count.ShouldBe(3);
        result2.Count.ShouldBe(3);
        // Provider should only be called once due to caching
        await _toolProvider.Received(1).ListToolsAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvalidateCache_ForcesRefreshOnNextCall()
    {
        var initialTools = CreateSampleTools();
        var updatedTools = new List<ToolDefinition>
        {
            new("scheduler.create", "Create a task", "scheduler", null)
        }.AsReadOnly();

        _toolProvider.ListToolsAsync(Arg.Any<CancellationToken>())
            .Returns(initialTools, updatedTools);

        var sut = CreateSut();

        // First call — caches initial tools
        var result1 = await sut.ListToolsAsync(cancellationToken: CancellationToken.None);
        result1.Count.ShouldBe(3);

        // Invalidate cache
        sut.InvalidateCache();

        // Second call — should fetch updated tools
        var result2 = await sut.ListToolsAsync(cancellationToken: CancellationToken.None);
        result2.Count.ShouldBe(1);
        result2[0].Name.ShouldBe("scheduler.create");

        // Provider called twice: initial + after invalidation
        await _toolProvider.Received(2).ListToolsAsync(Arg.Any<CancellationToken>());
    }

    #endregion

    #region ListToolsAsync — Persona Filtering

    [Fact]
    public async Task ListToolsAsync_WithPersona_AppliesFilter()
    {
        var tools = CreateSampleTools();
        _toolProvider.ListToolsAsync(Arg.Any<CancellationToken>()).Returns(tools);

        var filteredTools = new List<ToolDefinition>
        {
            tools[0] // Only knowhub.search
        }.AsReadOnly();

        _personaFilter.FilterAsync(Arg.Any<IReadOnlyList<ToolDefinition>>(), "reader", Arg.Any<CancellationToken>())
            .Returns(filteredTools);

        var sut = CreateSut();

        var result = await sut.ListToolsAsync("reader", CancellationToken.None);

        result.Count.ShouldBe(1);
        result[0].Name.ShouldBe("knowhub.search");
        await _personaFilter.Received(1).FilterAsync(
            Arg.Any<IReadOnlyList<ToolDefinition>>(), "reader", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListToolsAsync_WithoutPersona_SkipsFilter()
    {
        var tools = CreateSampleTools();
        _toolProvider.ListToolsAsync(Arg.Any<CancellationToken>()).Returns(tools);
        var sut = CreateSut();

        var result = await sut.ListToolsAsync(persona: null, cancellationToken: CancellationToken.None);

        result.Count.ShouldBe(3);
        await _personaFilter.DidNotReceive().FilterAsync(
            Arg.Any<IReadOnlyList<ToolDefinition>>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListToolsAsync_EmptyPersona_SkipsFilter()
    {
        var tools = CreateSampleTools();
        _toolProvider.ListToolsAsync(Arg.Any<CancellationToken>()).Returns(tools);
        var sut = CreateSut();

        var result = await sut.ListToolsAsync(persona: "  ", cancellationToken: CancellationToken.None);

        result.Count.ShouldBe(3);
        await _personaFilter.DidNotReceive().FilterAsync(
            Arg.Any<IReadOnlyList<ToolDefinition>>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    #endregion

    #region InvokeToolAsync — Routing

    [Fact]
    public async Task InvokeToolAsync_DelegatesToProvider()
    {
        var expectedResult = new ToolInvocationResult(true, "result data", null);
        _toolProvider.InvokeToolAsync("knowhub.search", Arg.Any<Dictionary<string, object>>(), Arg.Any<CancellationToken>())
            .Returns(expectedResult);
        var sut = CreateSut();
        var args = new Dictionary<string, object> { ["query"] = "test" };

        var result = await sut.InvokeToolAsync("knowhub.search", args, CancellationToken.None);

        result.Success.ShouldBeTrue();
        result.Content.ShouldBe("result data");
        result.Error.ShouldBeNull();
    }

    [Fact]
    public async Task InvokeToolAsync_ThrowsOnNullToolName()
    {
        var sut = CreateSut();

        await Should.ThrowAsync<ArgumentException>(
            () => sut.InvokeToolAsync(null!, new Dictionary<string, object>(), CancellationToken.None));
    }

    [Fact]
    public async Task InvokeToolAsync_ThrowsOnEmptyToolName()
    {
        var sut = CreateSut();

        await Should.ThrowAsync<ArgumentException>(
            () => sut.InvokeToolAsync("", new Dictionary<string, object>(), CancellationToken.None));
    }

    [Fact]
    public async Task InvokeToolAsync_ThrowsOnNullArguments()
    {
        var sut = CreateSut();

        await Should.ThrowAsync<ArgumentNullException>(
            () => sut.InvokeToolAsync("knowhub.search", null!, CancellationToken.None));
    }

    [Fact]
    public async Task InvokeToolAsync_CancellationRequested_Throws()
    {
        var sut = CreateSut();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Should.ThrowAsync<OperationCanceledException>(
            () => sut.InvokeToolAsync("knowhub.search", new Dictionary<string, object>(), cts.Token));
    }

    [Fact]
    public async Task InvokeToolAsync_PropagatesProviderError()
    {
        var errorResult = new ToolInvocationResult(false, null, "Tool not found");
        _toolProvider.InvokeToolAsync("unknown.tool", Arg.Any<Dictionary<string, object>>(), Arg.Any<CancellationToken>())
            .Returns(errorResult);
        var sut = CreateSut();

        var result = await sut.InvokeToolAsync("unknown.tool", new Dictionary<string, object>(), CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.Error.ShouldBe("Tool not found");
    }

    #endregion

    #region SearchToolsAsync

    [Fact]
    public async Task SearchToolsAsync_MatchesByName()
    {
        var tools = CreateSampleTools();
        _toolProvider.ListToolsAsync(Arg.Any<CancellationToken>()).Returns(tools);
        var sut = CreateSut();

        var result = await sut.SearchToolsAsync("search", cancellationToken: CancellationToken.None);

        result.Count.ShouldBe(1);
        result[0].Name.ShouldBe("knowhub.search");
    }

    [Fact]
    public async Task SearchToolsAsync_MatchesByDescription()
    {
        var tools = CreateSampleTools();
        _toolProvider.ListToolsAsync(Arg.Any<CancellationToken>()).Returns(tools);
        var sut = CreateSut();

        var result = await sut.SearchToolsAsync("upload", cancellationToken: CancellationToken.None);

        result.Count.ShouldBe(1);
        result[0].Name.ShouldBe("mediahub.upload");
    }

    [Fact]
    public async Task SearchToolsAsync_CaseInsensitive()
    {
        var tools = CreateSampleTools();
        _toolProvider.ListToolsAsync(Arg.Any<CancellationToken>()).Returns(tools);
        var sut = CreateSut();

        var result = await sut.SearchToolsAsync("SEARCH", cancellationToken: CancellationToken.None);

        result.Count.ShouldBe(1);
        result[0].Name.ShouldBe("knowhub.search");
    }

    [Fact]
    public async Task SearchToolsAsync_NoMatch_ReturnsEmpty()
    {
        var tools = CreateSampleTools();
        _toolProvider.ListToolsAsync(Arg.Any<CancellationToken>()).Returns(tools);
        var sut = CreateSut();

        var result = await sut.SearchToolsAsync("nonexistent", cancellationToken: CancellationToken.None);

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task SearchToolsAsync_ThrowsOnNullQuery()
    {
        var sut = CreateSut();

        await Should.ThrowAsync<ArgumentException>(
            () => sut.SearchToolsAsync(null!, cancellationToken: CancellationToken.None));
    }

    [Fact]
    public async Task SearchToolsAsync_WithPersona_FiltersBeforeSearch()
    {
        var tools = CreateSampleTools();
        _toolProvider.ListToolsAsync(Arg.Any<CancellationToken>()).Returns(tools);

        // Filter to only knowhub tools for "reader" persona
        var filteredTools = new List<ToolDefinition>
        {
            tools[0], tools[1]
        }.AsReadOnly();
        _personaFilter.FilterAsync(Arg.Any<IReadOnlyList<ToolDefinition>>(), "reader", Arg.Any<CancellationToken>())
            .Returns(filteredTools);

        var sut = CreateSut();

        var result = await sut.SearchToolsAsync("upload", "reader", CancellationToken.None);

        // "upload" matches mediahub.upload, but persona filter removed it
        result.ShouldBeEmpty();
    }

    #endregion

    #region ListToolsByServerAsync

    [Fact]
    public async Task ListToolsByServerAsync_FiltersCorrectly()
    {
        var tools = CreateSampleTools();
        _toolProvider.ListToolsAsync(Arg.Any<CancellationToken>()).Returns(tools);
        var sut = CreateSut();

        var result = await sut.ListToolsByServerAsync("knowhub", cancellationToken: CancellationToken.None);

        result.Count.ShouldBe(2);
        result.ShouldAllBe(t => t.ServerName == "knowhub");
    }

    [Fact]
    public async Task ListToolsByServerAsync_CaseInsensitive()
    {
        var tools = CreateSampleTools();
        _toolProvider.ListToolsAsync(Arg.Any<CancellationToken>()).Returns(tools);
        var sut = CreateSut();

        var result = await sut.ListToolsByServerAsync("KnowHub", cancellationToken: CancellationToken.None);

        result.Count.ShouldBe(2);
    }

    [Fact]
    public async Task ListToolsByServerAsync_UnknownServer_ReturnsEmpty()
    {
        var tools = CreateSampleTools();
        _toolProvider.ListToolsAsync(Arg.Any<CancellationToken>()).Returns(tools);
        var sut = CreateSut();

        var result = await sut.ListToolsByServerAsync("nonexistent", cancellationToken: CancellationToken.None);

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task ListToolsByServerAsync_ThrowsOnNullServerName()
    {
        var sut = CreateSut();

        await Should.ThrowAsync<ArgumentException>(
            () => sut.ListToolsByServerAsync(null!, cancellationToken: CancellationToken.None));
    }

    [Fact]
    public async Task ListToolsByServerAsync_WithPersona_FiltersBeforeServerFilter()
    {
        var tools = CreateSampleTools();
        _toolProvider.ListToolsAsync(Arg.Any<CancellationToken>()).Returns(tools);

        // Filter removes knowhub.ingest for "reader" persona
        var filteredTools = new List<ToolDefinition>
        {
            tools[0], // knowhub.search
            tools[2]  // mediahub.upload
        }.AsReadOnly();
        _personaFilter.FilterAsync(Arg.Any<IReadOnlyList<ToolDefinition>>(), "reader", Arg.Any<CancellationToken>())
            .Returns(filteredTools);

        var sut = CreateSut();

        var result = await sut.ListToolsByServerAsync("knowhub", "reader", CancellationToken.None);

        result.Count.ShouldBe(1);
        result[0].Name.ShouldBe("knowhub.search");
    }

    #endregion

    #region McpToolCatalogOptions

    [Fact]
    public void McpToolCatalogOptions_HasExpectedDefaults()
    {
        var options = new McpToolCatalogOptions();

        options.CacheTtl.ShouldBe(TimeSpan.FromMinutes(5));
        options.CacheKey.ShouldBe("mcp:tool-catalog:all");
    }

    #endregion

    #region DefaultPersonaToolFilter

    [Fact]
    public async Task DefaultPersonaToolFilter_ReturnsAllTools()
    {
        var filter = new DefaultPersonaToolFilter();
        var tools = CreateSampleTools();

        var result = await filter.FilterAsync(tools, "any-persona", CancellationToken.None);

        result.Count.ShouldBe(tools.Count);
    }

    [Fact]
    public async Task DefaultPersonaToolFilter_ThrowsOnNullTools()
    {
        var filter = new DefaultPersonaToolFilter();

        await Should.ThrowAsync<ArgumentNullException>(
            () => filter.FilterAsync(null!, "persona", CancellationToken.None));
    }

    [Fact]
    public async Task DefaultPersonaToolFilter_ThrowsOnNullPersona()
    {
        var filter = new DefaultPersonaToolFilter();

        await Should.ThrowAsync<ArgumentException>(
            () => filter.FilterAsync(CreateSampleTools(), null!, CancellationToken.None));
    }

    [Fact]
    public async Task DefaultPersonaToolFilter_ThrowsOnEmptyPersona()
    {
        var filter = new DefaultPersonaToolFilter();

        await Should.ThrowAsync<ArgumentException>(
            () => filter.FilterAsync(CreateSampleTools(), "  ", CancellationToken.None));
    }

    #endregion

    #region McpToolCatalogCacheInvalidatedEvent

    [Fact]
    public void McpToolCatalogCacheInvalidatedEvent_DefaultReasonIsNull()
    {
        var evt = new McpToolCatalogCacheInvalidatedEvent();

        evt.Reason.ShouldBeNull();
    }

    [Fact]
    public void McpToolCatalogCacheInvalidatedEvent_PreservesReason()
    {
        var evt = new McpToolCatalogCacheInvalidatedEvent("server-registered");

        evt.Reason.ShouldBe("server-registered");
    }

    #endregion

    #region McpToolCatalogCacheInvalidatedHandler

    [Fact]
    public void CacheInvalidatedHandler_InvokesCatalogInvalidation()
    {
        var catalogService = Substitute.For<IMcpToolCatalogService>();
        var handlerLogger = Substitute.For<ILogger<McpToolCatalogCacheInvalidatedHandler>>();
        var handler = new McpToolCatalogCacheInvalidatedHandler(catalogService, handlerLogger);

        handler.Handle(new McpToolCatalogCacheInvalidatedEvent("test-reason"));

        catalogService.Received(1).InvalidateCache();
    }

    [Fact]
    public void CacheInvalidatedHandler_ThrowsOnNullCatalogService()
    {
        var handlerLogger = Substitute.For<ILogger<McpToolCatalogCacheInvalidatedHandler>>();

        Should.Throw<ArgumentNullException>(() =>
            new McpToolCatalogCacheInvalidatedHandler(null!, handlerLogger));
    }

    [Fact]
    public void CacheInvalidatedHandler_ThrowsOnNullLogger()
    {
        var catalogService = Substitute.For<IMcpToolCatalogService>();

        Should.Throw<ArgumentNullException>(() =>
            new McpToolCatalogCacheInvalidatedHandler(catalogService, null!));
    }

    #endregion

    #region Helpers

    private McpToolCatalogService CreateSut(McpToolCatalogOptions? options = null)
    {
        return new McpToolCatalogService(
            _toolProvider,
            _personaFilter,
            _cache,
            options ?? new McpToolCatalogOptions(),
            _logger);
    }

    private static IReadOnlyList<ToolDefinition> CreateSampleTools()
    {
        return new List<ToolDefinition>
        {
            new("knowhub.search", "Search documents in KnowHub", "knowhub", null),
            new("knowhub.ingest", "Ingest a document into KnowHub", "knowhub", null),
            new("mediahub.upload", "Upload a media file to MediaHub", "mediahub", null)
        }.AsReadOnly();
    }

    #endregion
}
