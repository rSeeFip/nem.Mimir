using FluentValidation;
using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Domain.Entities;
using ChannelId = nem.Contracts.Identity.ChannelId;

namespace nem.Mimir.Application.Conversations.Commands;

public sealed record ShareConversationToChannelCommand(
    Guid ConversationId,
    Guid ChannelId,
    Guid? MessageId) : ICommand<ShareConversationToChannelDto>;

public sealed class ShareConversationToChannelCommandValidator : AbstractValidator<ShareConversationToChannelCommand>
{
    public ShareConversationToChannelCommandValidator()
    {
        RuleFor(x => x.ConversationId)
            .NotEmpty().WithMessage("Conversation ID is required.");

        RuleFor(x => x.ChannelId)
            .NotEmpty().WithMessage("Channel ID is required.");
    }
}

public sealed class ShareConversationToChannelHandler
{
    public static async Task<ShareConversationToChannelDto> Handle(
        ShareConversationToChannelCommand command,
        IConversationRepository conversationRepository,
        IChannelRepository channelRepository,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork,
        CancellationToken ct)
    {
        var userId = currentUser.UserId
            ?? throw new ForbiddenAccessException("User is not authenticated.");

        if (!Guid.TryParse(userId, out var userGuid))
            throw new ForbiddenAccessException("User identity could not be determined.");

        var conversation = await conversationRepository.GetWithMessagesAsync(command.ConversationId, ct).ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(Conversation), command.ConversationId);

        if (conversation.UserId != userGuid)
            throw new ForbiddenAccessException();

        var channel = await channelRepository.GetWithMembersAsync(ChannelId.From(command.ChannelId), ct).ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(Channel), command.ChannelId);

        if (!channel.Members.Any(member => member.UserId == userGuid && member.LeftAt is null))
            throw new ForbiddenAccessException();

        var messageCount = conversation.Messages.Count;
        var permalink = $"/mimir/conversations/{conversation.Id}";

        var sharedContent = command.MessageId.HasValue
            ? BuildSharedMessageContent(conversation, command.MessageId.Value, permalink)
            : $"Shared conversation: {conversation.Title}\nMessages: {messageCount}\nLink: {permalink}";

        var sharedMessage = channel.AddMessage(userGuid, sharedContent);

        await channelRepository.UpdateAsync(channel, ct).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);

        return new ShareConversationToChannelDto(
            command.ConversationId,
            command.ChannelId,
            sharedMessage.Id.Value,
            command.MessageId.HasValue);
    }

    private static string BuildSharedMessageContent(Conversation conversation, Guid messageId, string permalink)
    {
        var message = conversation.Messages.FirstOrDefault(item => item.Id == messageId)
            ?? throw new NotFoundException(nameof(Message), messageId);

        return $"Shared message from conversation: {conversation.Title}\n" +
               $"Role: {message.Role}\n" +
               $"Content: {message.Content}\n" +
               $"Link: {permalink}#message-{messageId}";
    }
}
