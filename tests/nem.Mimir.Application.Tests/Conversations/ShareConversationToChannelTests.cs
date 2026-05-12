using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Conversations.Commands;
using nem.Mimir.Domain.Entities;
using nem.Mimir.Domain.Enums;
using NSubstitute;
using Shouldly;

namespace nem.Mimir.Application.Tests.Conversations;

public sealed class ShareConversationToChannelTests
{
    [Fact]
    public async Task Handle_ValidShare_ShouldCreateChannelMessage()
    {
        var conversationRepository = Substitute.For<IConversationRepository>();
        var channelRepository = Substitute.For<IChannelRepository>();
        var currentUser = Substitute.For<ICurrentUserService>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        var userId = Guid.NewGuid();
        currentUser.UserId.Returns(userId.ToString());

        var conversation = Conversation.Create(userId, "To Share");
        conversation.AddMessage(MessageRole.User, "Question");
        conversation.AddMessage(MessageRole.Assistant, "Answer", "phi-4-mini");
        conversationRepository.GetWithMessagesAsync(conversation.Id, Arg.Any<CancellationToken>()).Returns(conversation);

        var channel = Channel.Create(userId, "general", "desc", ChannelType.Public);
        channelRepository.GetWithMembersAsync(Arg.Any<nem.Contracts.Identity.ChannelId>(), Arg.Any<CancellationToken>()).Returns(channel);

        var result = await ShareConversationToChannelHandler.Handle(
            new ShareConversationToChannelCommand(conversation.Id, channel.Id.Value, null),
            conversationRepository,
            channelRepository,
            currentUser,
            unitOfWork,
            CancellationToken.None);

        result.ConversationId.ShouldBe(conversation.Id);
        result.ChannelId.ShouldBe(channel.Id.Value);
        result.IsMessageShare.ShouldBeFalse();

        await channelRepository.Received(1).UpdateAsync(channel, Arg.Any<CancellationToken>());
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenUserNotMemberOfChannel_ShouldThrowForbidden()
    {
        var conversationRepository = Substitute.For<IConversationRepository>();
        var channelRepository = Substitute.For<IChannelRepository>();
        var currentUser = Substitute.For<ICurrentUserService>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        var userId = Guid.NewGuid();
        currentUser.UserId.Returns(userId.ToString());

        var conversation = Conversation.Create(userId, "To Share");
        conversationRepository.GetWithMessagesAsync(conversation.Id, Arg.Any<CancellationToken>()).Returns(conversation);

        var otherUserChannel = Channel.Create(Guid.NewGuid(), "other", "desc", ChannelType.Public);
        channelRepository.GetWithMembersAsync(Arg.Any<nem.Contracts.Identity.ChannelId>(), Arg.Any<CancellationToken>()).Returns(otherUserChannel);

        await Should.ThrowAsync<ForbiddenAccessException>(() =>
            ShareConversationToChannelHandler.Handle(
                new ShareConversationToChannelCommand(conversation.Id, otherUserChannel.Id.Value, null),
                conversationRepository,
                channelRepository,
                currentUser,
                unitOfWork,
                CancellationToken.None));
    }
}
