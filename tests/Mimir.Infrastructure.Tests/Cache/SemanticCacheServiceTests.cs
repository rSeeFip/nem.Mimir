using Microsoft.Extensions.Logging;
using Mimir.Infrastructure.Cache;
using NSubstitute;
using Shouldly;

namespace Mimir.Infrastructure.Tests.Cache;

public sealed class SemanticCacheServiceTests
{
    private readonly ILogger<SemanticCacheService> _logger;

    public SemanticCacheServiceTests()
    {
        _logger = Substitute.For<ILogger<SemanticCacheService>>();
    }

    [Fact]
    public async Task GetAsync_WhenSemanticMatchExists_ReturnsCachedValue()
    {
        var sut = CreateSut();

        await sut.SetAsync("How to bake sourdough bread?", "Use high hydration dough and long fermentation.");

        var value = await sut.GetAsync("how to bake sourdough bread?", 0.95f);

        value.ShouldBe("Use high hydration dough and long fermentation.");
    }

    [Fact]
    public async Task GetAsync_WhenNoSemanticMatch_ReturnsNull()
    {
        var sut = CreateSut();

        await sut.SetAsync("book a flight to tokyo", "Try dates with cheaper weekdays.");

        var value = await sut.GetAsync("what is the weather in berlin", 0.95f);

        value.ShouldBeNull();
    }

    [Fact]
    public async Task SetAsyncAndGetAsync_Roundtrip_ReturnsStoredValue()
    {
        var sut = CreateSut();

        await sut.SetAsync("Explain binary search", "Binary search halves sorted search space each step.");

        var value = await sut.GetAsync("Explain binary search", 0.99f);

        value.ShouldBe("Binary search halves sorted search space each step.");
    }

    [Fact]
    public async Task GetAsync_WhenEntryExpired_ReturnsNull()
    {
        var sut = CreateSut();

        await sut.SetAsync("cached prompt", "cached response", TimeSpan.FromMilliseconds(20));
        await Task.Delay(60);

        var value = await sut.GetAsync("cached prompt", 0.95f);

        value.ShouldBeNull();
    }

    [Fact]
    public async Task GetStatsAsync_ReturnsAccurateCountersAndHitRate()
    {
        var sut = CreateSut();

        await sut.SetAsync("semantic key one", "response one");

        (await sut.GetAsync("semantic key one", 0.95f)).ShouldNotBeNull();
        (await sut.GetAsync("another unrelated key", 0.95f)).ShouldBeNull();
        (await sut.GetAsync("third unrelated key", 0.95f)).ShouldBeNull();

        var stats = await sut.GetStatsAsync();

        stats.TotalRequests.ShouldBe(3);
        stats.Hits.ShouldBe(1);
        stats.Misses.ShouldBe(2);
        stats.HitRate.ShouldBe(1d / 3d, 0.000001d);
        stats.EstimatedSizeBytes.ShouldBeGreaterThan(0);
    }

    private SemanticCacheService CreateSut(SemanticCacheOptions? options = null) =>
        new(options ?? new SemanticCacheOptions(), _logger);
}
