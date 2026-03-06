using Microsoft.EntityFrameworkCore;
using Mimir.Infrastructure.Identity;
using Mimir.Infrastructure.Persistence;
using nem.Contracts.Channels;
using nem.Contracts.Identity;
using Shouldly;

namespace Mimir.Application.Tests.Identity;

public sealed class ActorIdentityPersistenceTests : IDisposable
{
    private readonly MimirDbContext _context;

    public ActorIdentityPersistenceTests()
    {
        var options = new DbContextOptionsBuilder<MimirDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new MimirDbContext(options);
    }

    public void Dispose() => _context.Dispose();

    // ──────────────────────────────────────────────────────────────────────
    // EfCoreActorIdentityResolver
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ResolveAsync_FindsByChannelTypeAndProviderUserId()
    {
        var userId = Guid.NewGuid();
        var doc = new ActorIdentityDocument
        {
            Id = userId,
            DisplayName = "Alice",
            Email = "alice@example.com",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        doc.Links.Add(new ChannelIdentityLinkDocument
        {
            Id = Guid.NewGuid(),
            ActorIdentityId = userId,
            ChannelType = ChannelType.Telegram,
            ProviderUserId = "tg-alice",
            TrustLevel = TrustLevel.Verified,
            LinkedAt = DateTime.UtcNow,
            IsActive = true,
        });

        _context.ActorIdentities.Add(doc);
        await _context.SaveChangesAsync();

        var resolver = new EfCoreActorIdentityResolver(_context);
        var result = await resolver.ResolveAsync(ChannelType.Telegram, "tg-alice");

        result.ShouldNotBeNull();
        result.InternalUserId.ShouldBe(userId);
        result.DisplayName.ShouldBe("Alice");
        result.Email.ShouldBe("alice@example.com");
    }

    [Fact]
    public async Task ResolveAsync_ReturnsNull_ForUnknownIdentity()
    {
        var resolver = new EfCoreActorIdentityResolver(_context);
        var result = await resolver.ResolveAsync(ChannelType.Teams, "nonexistent");

        result.ShouldBeNull();
    }

    [Fact]
    public async Task ResolveAsync_IgnoresInactiveLinks()
    {
        var userId = Guid.NewGuid();
        var doc = new ActorIdentityDocument
        {
            Id = userId,
            DisplayName = "Bob",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        doc.Links.Add(new ChannelIdentityLinkDocument
        {
            Id = Guid.NewGuid(),
            ActorIdentityId = userId,
            ChannelType = ChannelType.WhatsApp,
            ProviderUserId = "wa-bob",
            TrustLevel = TrustLevel.PhoneVerified,
            LinkedAt = DateTime.UtcNow,
            IsActive = false,
        });

        _context.ActorIdentities.Add(doc);
        await _context.SaveChangesAsync();

        var resolver = new EfCoreActorIdentityResolver(_context);
        var result = await resolver.ResolveAsync(ChannelType.WhatsApp, "wa-bob");

        result.ShouldBeNull();
    }

    // ──────────────────────────────────────────────────────────────────────
    // EfCoreActorIdentityService
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task LinkChannelAsync_CreatesNewActorAndLink()
    {
        var service = new EfCoreActorIdentityService(_context);
        var userId = Guid.NewGuid();
        var link = new ChannelIdentityLink
        {
            ChannelType = ChannelType.Telegram,
            ProviderUserId = "tg-new",
            TrustLevel = TrustLevel.SessionBased,
        };

        var result = await service.LinkChannelAsync(userId, link);

        result.ShouldNotBeNull();
        result.InternalUserId.ShouldBe(userId);
        result.Links.ShouldHaveSingleItem();
        result.Links[0].ChannelType.ShouldBe(ChannelType.Telegram);
        result.Links[0].ProviderUserId.ShouldBe("tg-new");
    }

    [Fact]
    public async Task LinkChannelAsync_UpdatesExistingLink()
    {
        var service = new EfCoreActorIdentityService(_context);
        var userId = Guid.NewGuid();

        var initialLink = new ChannelIdentityLink
        {
            ChannelType = ChannelType.Signal,
            ProviderUserId = "sig-1",
            TrustLevel = TrustLevel.SessionBased,
        };
        await service.LinkChannelAsync(userId, initialLink);

        var updatedLink = new ChannelIdentityLink
        {
            ChannelType = ChannelType.Signal,
            ProviderUserId = "sig-1",
            TrustLevel = TrustLevel.Verified,
        };
        var result = await service.LinkChannelAsync(userId, updatedLink);

        result.Links.Count.ShouldBe(1);
        result.Links[0].TrustLevel.ShouldBe(TrustLevel.Verified);
    }

    [Fact]
    public async Task UnlinkChannelAsync_SoftDeletesSetsIsActiveFalse()
    {
        var service = new EfCoreActorIdentityService(_context);
        var userId = Guid.NewGuid();

        var link = new ChannelIdentityLink
        {
            ChannelType = ChannelType.Teams,
            ProviderUserId = "teams-user",
            TrustLevel = TrustLevel.Verified,
        };
        await service.LinkChannelAsync(userId, link);

        await service.UnlinkChannelAsync(userId, ChannelType.Teams, "teams-user");

        var linkDoc = await _context.ChannelIdentityLinks
            .FirstOrDefaultAsync(l => l.ProviderUserId == "teams-user");

        linkDoc.ShouldNotBeNull();
        linkDoc.IsActive.ShouldBeFalse();
    }

    [Fact]
    public async Task UnlinkChannelAsync_NoOp_ForNonexistentLink()
    {
        var service = new EfCoreActorIdentityService(_context);

        // Should not throw
        await service.UnlinkChannelAsync(Guid.NewGuid(), ChannelType.Telegram, "nonexistent");
    }

    [Fact]
    public async Task GetLinksAsync_ReturnsActiveLinksOnly()
    {
        var service = new EfCoreActorIdentityService(_context);
        var userId = Guid.NewGuid();

        await service.LinkChannelAsync(userId, new ChannelIdentityLink
        {
            ChannelType = ChannelType.Telegram,
            ProviderUserId = "tg-1",
        });
        await service.LinkChannelAsync(userId, new ChannelIdentityLink
        {
            ChannelType = ChannelType.WhatsApp,
            ProviderUserId = "wa-1",
        });

        await service.UnlinkChannelAsync(userId, ChannelType.Telegram, "tg-1");

        var links = await service.GetLinksAsync(userId);

        links.Count.ShouldBe(1);
        links[0].ChannelType.ShouldBe(ChannelType.WhatsApp);
        links[0].ProviderUserId.ShouldBe("wa-1");
    }

    [Fact]
    public async Task GetLinksAsync_ReturnsEmpty_ForUnknownUser()
    {
        var service = new EfCoreActorIdentityService(_context);
        var links = await service.GetLinksAsync(Guid.NewGuid());

        links.ShouldBeEmpty();
    }
}
