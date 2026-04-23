using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using nem.Contracts.Identity;
using nem.Contracts.SessionPromotion;
using nem.Mimir.Infrastructure.Cache;
using NSubstitute;
using Shouldly;
using System.Security.Claims;

namespace nem.Mimir.Infrastructure.Tests.Cache;

[Trait("Category", "CacheIsolation")]
public sealed class TenantCacheIsolationTests
{
    [Fact]
    public async Task SameCacheKey_DifferentTenants_StoredSeparately()
    {
        var (cacheA, _) = CreateCacheWithUser("user-1", "tenant-a");
        var (cacheB, _) = CreateCacheWithUser("user-1", "tenant-b");

        await cacheA.SetAsync("hello", "value-for-tenant-a");
        await cacheB.SetAsync("hello", "value-for-tenant-b");

        var resultA = await cacheA.GetAsync("hello");
        var resultB = await cacheB.GetAsync("hello");

        resultA.ShouldBe("value-for-tenant-a");
        resultB.ShouldBe("value-for-tenant-b");
    }

    [Fact]
    public async Task TenantA_CannotRead_TenantBEntry()
    {
        var (cacheA, _) = CreateCacheWithUser("user-1", "tenant-a");
        var (cacheB, _) = CreateCacheWithUser("user-1", "tenant-b");

        await cacheB.SetAsync("secret", "tenant-b-secret");

        var resultFromA = await cacheA.GetAsync("secret");

        resultFromA.ShouldBeNull();
    }

    [Fact]
    public async Task InvalidateTenantA_DoesNotAffect_TenantB()
    {
        var (cacheA, _) = CreateCacheWithUser("user-1", "tenant-a");
        var (cacheB, _) = CreateCacheWithUser("user-1", "tenant-b");

        await cacheA.SetAsync("shared-key", "a-value");
        await cacheB.SetAsync("shared-key", "b-value");

        await cacheA.InvalidateAsync("shared-key");

        var resultA = await cacheA.GetAsync("shared-key");
        var resultB = await cacheB.GetAsync("shared-key");

        resultA.ShouldBeNull();
        resultB.ShouldBe("b-value");
    }

    private static (SemanticCacheService cache, IHttpContextAccessor accessor) CreateCacheWithUser(
        string userId, string tenantId)
    {
        var options = new SemanticCacheOptions
        {
            EnablePerUserIsolation = true,
            EnableTenantIsolation = true,
        };

        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        var httpContext = Substitute.For<HttpContext>();
        var claims = new List<Claim>
        {
            new("sub", userId),
            new("tenant_id", tenantId),
        };
        var identity = new ClaimsIdentity(claims, "test");
        var principal = new ClaimsPrincipal(identity);
        httpContext.User.Returns(principal);
        httpContextAccessor.HttpContext.Returns(httpContext);

        var cache = new SemanticCacheService(options, NullLogger<SemanticCacheService>.Instance, httpContextAccessor);
        return (cache, httpContextAccessor);
    }
}

[Trait("Category", "CacheInvalidationOnPromotion")]
public sealed class SessionPromotedCacheInvalidationTests
{
    [Fact]
    public async Task Handle_SessionPromotedEvent_InvalidatesOldKey_SeedsNewKey()
    {
        var cache = Substitute.For<nem.Contracts.TokenOptimization.ISemanticCache>();
        var logger = NullLogger<object>.Instance;

        var oldChannelId = ChannelId.New();
        var newChannelId = ChannelId.New();
        var forkId = ConversationForkId.New();
        var evt = new SessionPromotedEvent(oldChannelId, newChannelId, forkId, DateTimeOffset.UtcNow);

        await nem.Mimir.Infrastructure.SessionPromotion.SessionPromotedEventHandler.HandleAsync(
            evt, cache, logger, CancellationToken.None);

        await cache.Received(1).InvalidateAsync($"session:{forkId}:{oldChannelId}", Arg.Any<CancellationToken>());
        await cache.Received(1).SetAsync(
            $"session:{forkId}:{newChannelId}",
            Arg.Any<string>(),
            Arg.Any<TimeSpan?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NullEvent_ThrowsArgumentNullException()
    {
        var cache = Substitute.For<nem.Contracts.TokenOptimization.ISemanticCache>();
        var logger = NullLogger<object>.Instance;

        await Should.ThrowAsync<ArgumentNullException>(
            () => nem.Mimir.Infrastructure.SessionPromotion.SessionPromotedEventHandler.HandleAsync(
                null!, cache, logger, CancellationToken.None));
    }
}
