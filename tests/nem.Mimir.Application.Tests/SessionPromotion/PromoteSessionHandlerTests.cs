using Marten;
using nem.Contracts.Identity;
using nem.Contracts.SessionPromotion;
using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.SessionPromotion;
using nem.Mimir.Domain.Entities;
using nem.Mimir.Domain.Enums;
using NSubstitute;
using Shouldly;

namespace nem.Mimir.Application.Tests.SessionPromotion;

public sealed class PromoteSessionHandlerTests
{
    [Fact]
    public async Task Handle_ValidPromotion_ReturnsSessionPromotedEvent()
    {
        var oldChannelId = ChannelId.New();
        var newChannelId = ChannelId.New();
        var forkId = ConversationForkId.New();

        var channelRepository = Substitute.For<IChannelRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var documentSession = Substitute.For<IDocumentSession>();

        channelRepository.GetByIdAsync(oldChannelId, Arg.Any<CancellationToken>())
            .Returns(CreateChannel("whatsapp"));
        channelRepository.GetByIdAsync(newChannelId, Arg.Any<CancellationToken>())
            .Returns(CreateChannel("teams"));

        var command = new PromoteSessionCommand(oldChannelId, newChannelId, forkId);

        var result = await PromoteSessionHandler.Handle(command, channelRepository, unitOfWork, documentSession, CancellationToken.None);

        result.OldChannelId.ShouldBe(oldChannelId);
        result.NewChannelId.ShouldBe(newChannelId);
        result.ConversationForkId.ShouldBe(forkId);
        result.PromotedAtUtc.ShouldBeGreaterThan(DateTimeOffset.UtcNow.AddSeconds(-5));
    }

    [Fact]
    public async Task Handle_SameChannelId_ThrowsValidationException()
    {
        var channelId = ChannelId.New();
        var forkId = ConversationForkId.New();

        var channelRepository = Substitute.For<IChannelRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var documentSession = Substitute.For<IDocumentSession>();

        var command = new PromoteSessionCommand(channelId, channelId, forkId);

        await Should.ThrowAsync<ValidationException>(
            () => PromoteSessionHandler.Handle(command, channelRepository, unitOfWork, documentSession, CancellationToken.None));

        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_OldChannelNotFound_ThrowsNotFoundException()
    {
        var oldChannelId = ChannelId.New();
        var newChannelId = ChannelId.New();
        var forkId = ConversationForkId.New();

        var channelRepository = Substitute.For<IChannelRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var documentSession = Substitute.For<IDocumentSession>();

        channelRepository.GetByIdAsync(oldChannelId, Arg.Any<CancellationToken>())
            .Returns((Channel?)null);

        var command = new PromoteSessionCommand(oldChannelId, newChannelId, forkId);

        await Should.ThrowAsync<NotFoundException>(
            () => PromoteSessionHandler.Handle(command, channelRepository, unitOfWork, documentSession, CancellationToken.None));

        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NewChannelNotFound_ThrowsNotFoundException()
    {
        var oldChannelId = ChannelId.New();
        var newChannelId = ChannelId.New();
        var forkId = ConversationForkId.New();

        var channelRepository = Substitute.For<IChannelRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var documentSession = Substitute.For<IDocumentSession>();

        channelRepository.GetByIdAsync(oldChannelId, Arg.Any<CancellationToken>())
            .Returns(CreateChannel("whatsapp"));
        channelRepository.GetByIdAsync(newChannelId, Arg.Any<CancellationToken>())
            .Returns((Channel?)null);

        var command = new PromoteSessionCommand(oldChannelId, newChannelId, forkId);

        await Should.ThrowAsync<NotFoundException>(
            () => PromoteSessionHandler.Handle(command, channelRepository, unitOfWork, documentSession, CancellationToken.None));

        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NullCommand_ThrowsArgumentNullException()
    {
        var channelRepository = Substitute.For<IChannelRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var documentSession = Substitute.For<IDocumentSession>();

        await Should.ThrowAsync<ArgumentNullException>(
            () => PromoteSessionHandler.Handle(null!, channelRepository, unitOfWork, documentSession, CancellationToken.None));
    }

    private static Channel CreateChannel(string name)
        => Channel.Create(Guid.NewGuid(), name, null, ChannelType.Public);
}
