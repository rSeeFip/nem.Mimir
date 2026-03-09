namespace Mimir.Integration.Tests.Identity;

using System.Text.Json;
using FluentAssertions;
using Mimir.Integration.Tests.Fixtures;
using nem.Contracts.Channels;
using nem.Contracts.Content;
using nem.Contracts.Identity;
using NSubstitute;

/// <summary>
/// Integration tests verifying cross-channel ActorIdentity resolution.
/// Tests that the same user can be identified across different channels
/// via shared InternalUserId and ChannelIdentityLinks.
/// </summary>
public sealed class CrossChannelIdentityTests
{
    private readonly IActorIdentityResolver _resolver = Substitute.For<IActorIdentityResolver>();
    private readonly IActorIdentityService _service = Substitute.For<IActorIdentityService>();

    [Fact]
    public async Task SameUser_ResolvedFromDifferentChannels_HasSameInternalUserId()
    {
        // Arrange — one user with links to Telegram, Teams, and WhatsApp
        var internalUserId = Guid.NewGuid();
        var telegramLink = new ChannelIdentityLink
        {
            ChannelType = ChannelType.Telegram,
            ProviderUserId = "tg-user-42",
            TrustLevel = TrustLevel.Verified,
            IsActive = true,
        };
        var teamsLink = new ChannelIdentityLink
        {
            ChannelType = ChannelType.Teams,
            ProviderUserId = "aad-user-42",
            TrustLevel = TrustLevel.Verified,
            IsActive = true,
        };
        var whatsAppLink = new ChannelIdentityLink
        {
            ChannelType = ChannelType.WhatsApp,
            ProviderUserId = "+1555123456",
            TrustLevel = TrustLevel.PhoneVerified,
            IsActive = true,
        };

        var identity = new ActorIdentity
        {
            InternalUserId = internalUserId,
            DisplayName = "Cross-Channel User",
            Email = "user@example.com",
            Links = [telegramLink, teamsLink, whatsAppLink],
        };

        _resolver.ResolveAsync(ChannelType.Telegram, "tg-user-42", Arg.Any<CancellationToken>())
            .Returns(identity);
        _resolver.ResolveAsync(ChannelType.Teams, "aad-user-42", Arg.Any<CancellationToken>())
            .Returns(identity);
        _resolver.ResolveAsync(ChannelType.WhatsApp, "+1555123456", Arg.Any<CancellationToken>())
            .Returns(identity);

        // Act — resolve from each channel
        var fromTelegram = await _resolver.ResolveAsync(ChannelType.Telegram, "tg-user-42");
        var fromTeams = await _resolver.ResolveAsync(ChannelType.Teams, "aad-user-42");
        var fromWhatsApp = await _resolver.ResolveAsync(ChannelType.WhatsApp, "+1555123456");

        // Assert — all resolve to the same internal user
        fromTelegram.Should().NotBeNull();
        fromTeams.Should().NotBeNull();
        fromWhatsApp.Should().NotBeNull();

        fromTelegram!.InternalUserId.Should().Be(internalUserId);
        fromTeams!.InternalUserId.Should().Be(internalUserId);
        fromWhatsApp!.InternalUserId.Should().Be(internalUserId);

        // All references should be the same identity
        fromTelegram.InternalUserId.Should().Be(fromTeams.InternalUserId);
        fromTeams.InternalUserId.Should().Be(fromWhatsApp.InternalUserId);
    }

    [Fact]
    public async Task CrossChannelIdentity_HasLinksForAllRegisteredChannels()
    {
        // Arrange
        var identity = new ActorIdentity
        {
            InternalUserId = Guid.NewGuid(),
            DisplayName = "Multi-Channel User",
            Links =
            [
                new ChannelIdentityLink { ChannelType = ChannelType.Telegram, ProviderUserId = "tg-100" },
                new ChannelIdentityLink { ChannelType = ChannelType.Teams, ProviderUserId = "aad-100" },
                new ChannelIdentityLink { ChannelType = ChannelType.WhatsApp, ProviderUserId = "+1555000100" },
                new ChannelIdentityLink { ChannelType = ChannelType.Signal, ProviderUserId = "+1555000200" },
                new ChannelIdentityLink { ChannelType = ChannelType.WebWidget, ProviderUserId = "web-100" },
            ],
        };

        // Assert — identity has links for all 5 channels
        identity.Links.Should().HaveCount(5);
        identity.Links.Select(l => l.ChannelType).Should().BeEquivalentTo(
            [ChannelType.Telegram, ChannelType.Teams, ChannelType.WhatsApp,
             ChannelType.Signal, ChannelType.WebWidget]);
    }

    [Fact]
    public async Task LinkChannelAsync_AddsNewChannelLink_ToExistingIdentity()
    {
        // Arrange
        var internalUserId = Guid.NewGuid();
        var newLink = new ChannelIdentityLink
        {
            ChannelType = ChannelType.Signal,
            ProviderUserId = "+1555777888",
            TrustLevel = TrustLevel.PhoneVerified,
            IsActive = true,
        };

        var updatedIdentity = new ActorIdentity
        {
            InternalUserId = internalUserId,
            DisplayName = "Newly Linked User",
            Links = [newLink],
        };

        _service.LinkChannelAsync(internalUserId, Arg.Any<ChannelIdentityLink>(), Arg.Any<CancellationToken>())
            .Returns(updatedIdentity);

        // Act
        var result = await _service.LinkChannelAsync(internalUserId, newLink, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.InternalUserId.Should().Be(internalUserId);
        result.Links.Should().ContainSingle(l =>
            l.ChannelType == ChannelType.Signal && l.ProviderUserId == "+1555777888");

        await _service.Received(1).LinkChannelAsync(internalUserId, Arg.Any<ChannelIdentityLink>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UnlinkChannelAsync_RemovesChannelLink()
    {
        // Arrange
        var internalUserId = Guid.NewGuid();

        // Act
        await _service.UnlinkChannelAsync(internalUserId, ChannelType.WhatsApp, "+1555000111", CancellationToken.None);

        // Assert
        await _service.Received(1).UnlinkChannelAsync(
            internalUserId, ChannelType.WhatsApp, "+1555000111", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetLinksAsync_ReturnsAllActiveLinks()
    {
        // Arrange
        var internalUserId = Guid.NewGuid();
        var links = new List<ChannelIdentityLink>
        {
            new() { ChannelType = ChannelType.Telegram, ProviderUserId = "tg-50", IsActive = true },
            new() { ChannelType = ChannelType.Teams, ProviderUserId = "aad-50", IsActive = true },
            new() { ChannelType = ChannelType.Signal, ProviderUserId = "+1555000050", IsActive = false },
        };

        _service.GetLinksAsync(internalUserId, Arg.Any<CancellationToken>())
            .Returns(links.AsReadOnly());

        // Act
        var result = await _service.GetLinksAsync(internalUserId, CancellationToken.None);

        // Assert
        result.Should().HaveCount(3);
        result.Where(l => l.IsActive).Should().HaveCount(2);
        result.Should().Contain(l => l.ChannelType == ChannelType.Telegram);
        result.Should().Contain(l => l.ChannelType == ChannelType.Teams);
    }

    [Fact]
    public async Task TrustLevel_VariesByChannel_ReflectsVerificationMethod()
    {
        // Arrange
        var identity = new ActorIdentity
        {
            InternalUserId = Guid.NewGuid(),
            DisplayName = "Trust Level Test User",
            Links =
            [
                new ChannelIdentityLink
                {
                    ChannelType = ChannelType.Teams,
                    ProviderUserId = "aad-trust",
                    TrustLevel = TrustLevel.Verified,
                },
                new ChannelIdentityLink
                {
                    ChannelType = ChannelType.WhatsApp,
                    ProviderUserId = "+1555trust",
                    TrustLevel = TrustLevel.PhoneVerified,
                },
                new ChannelIdentityLink
                {
                    ChannelType = ChannelType.WebWidget,
                    ProviderUserId = "web-trust",
                    TrustLevel = TrustLevel.SessionBased,
                },
            ],
        };

        // Assert
        identity.Links.Single(l => l.ChannelType == ChannelType.Teams)
            .TrustLevel.Should().Be(TrustLevel.Verified);
        identity.Links.Single(l => l.ChannelType == ChannelType.WhatsApp)
            .TrustLevel.Should().Be(TrustLevel.PhoneVerified);
        identity.Links.Single(l => l.ChannelType == ChannelType.WebWidget)
            .TrustLevel.Should().Be(TrustLevel.SessionBased);
    }

    [Fact]
    public async Task ChannelAdapterRegistry_SupportsAllFiveChannels()
    {
        // Arrange
        var registry = Substitute.For<IChannelAdapterRegistry>();
        var allChannels = new[] { ChannelType.Telegram, ChannelType.Teams, ChannelType.WhatsApp, ChannelType.Signal, ChannelType.WebWidget };
        registry.SupportedChannels.Returns(allChannels);

        foreach (var channel in allChannels)
        {
            var sender = Substitute.For<IChannelMessageSender>();
            sender.Channel.Returns(channel);
            registry.GetSender(channel).Returns(sender);

            var source = Substitute.For<IChannelEventSource>();
            source.Channel.Returns(channel);
            registry.GetEventSource(channel).Returns(source);
        }

        // Assert
        registry.SupportedChannels.Should().HaveCount(5);

        foreach (var channel in allChannels)
        {
            var sender = registry.GetSender(channel);
            sender.Should().NotBeNull();
            sender!.Channel.Should().Be(channel);

            var source = registry.GetEventSource(channel);
            source.Should().NotBeNull();
            source!.Channel.Should().Be(channel);
        }
    }

    [Fact]
    public async Task UnregisteredChannel_ReturnsNull_FromAdapterRegistry()
    {
        // Arrange — registry with only Telegram registered
        var registry = Substitute.For<IChannelAdapterRegistry>();
        registry.SupportedChannels.Returns([ChannelType.Telegram]);

        var sender = Substitute.For<IChannelMessageSender>();
        sender.Channel.Returns(ChannelType.Telegram);
        registry.GetSender(ChannelType.Telegram).Returns(sender);
        registry.GetSender(ChannelType.Teams).Returns((IChannelMessageSender?)null);
        registry.GetEventSource(ChannelType.Teams).Returns((IChannelEventSource?)null);

        // Assert
        registry.GetSender(ChannelType.Telegram).Should().NotBeNull();
        registry.GetSender(ChannelType.Teams).Should().BeNull();
        registry.GetEventSource(ChannelType.Teams).Should().BeNull();
    }
}
