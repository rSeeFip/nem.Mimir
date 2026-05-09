using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Conversations.Commands;
using nem.Mimir.Domain.Entities;
using nem.Mimir.Domain.Enums;
using NSubstitute;
using Shouldly;

namespace nem.Mimir.Application.Tests.Conversations;

public sealed class ConvertConversationToChannelTests
{
    [Fact]
    public async Task Handle_ValidRequest_ShouldCreateChannelAndArchiveConversation()
    {
        var conversationRepository = Substitute.For<IConversationRepository>();
        var channelRepository = Substitute.For<IChannelRepository>();
        var currentUser = Substitute.For<ICurrentUserService>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        var userId = Guid.NewGuid();
        currentUser.UserId.Returns(userId.ToString());

        var conversation = Conversation.Create(userId, "My Conversation");
        conversation.AddMessage(MessageRole.User, "Hello");
        conversation.AddMessage(MessageRole.Assistant, "Hi there", "phi-4-mini");
        conversationRepository.GetWithMessagesAsync(conversation.Id, Arg.Any<CancellationToken>()).Returns(conversation);

        var result = await ConvertConversationToChannelHandler.Handle(
            new ConvertConversationToChannelCommand(conversation.Id, "Converted Channel"),
            conversationRepository,
            channelRepository,
            currentUser,
            unitOfWork,
            CancellationToken.None);

        result.ConversationId.ShouldBe(conversation.Id);
        result.ChannelName.ShouldBe("Converted Channel");

        conversation.Status.ShouldBe(ConversationStatus.Archived);

        await channelRepository.Received(1).CreateAsync(Arg.Any<Channel>(), Arg.Any<CancellationToken>());
        await conversationRepository.Received(1).UpdateAsync(conversation, Arg.Any<CancellationToken>());
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ConversationNotOwned_ShouldThrowForbidden()
    {
        var conversationRepository = Substitute.For<IConversationRepository>();
        var channelRepository = Substitute.For<IChannelRepository>();
        var currentUser = Substitute.For<ICurrentUserService>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        currentUser.UserId.Returns(Guid.NewGuid().ToString());

        var conversation = Conversation.Create(Guid.NewGuid(), "Other");
        conversationRepository.GetWithMessagesAsync(conversation.Id, Arg.Any<CancellationToken>()).Returns(conversation);

        await Should.ThrowAsync<ForbiddenAccessException>(() =>
            ConvertConversationToChannelHandler.Handle(
                new ConvertConversationToChannelCommand(conversation.Id, null),
                conversationRepository,
                channelRepository,
                currentUser,
                unitOfWork,
                CancellationToken.None));
    }
}
