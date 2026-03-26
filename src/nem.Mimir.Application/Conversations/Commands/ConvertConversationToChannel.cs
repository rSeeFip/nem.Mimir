using FluentValidation;
using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Domain.Entities;
using nem.Mimir.Domain.Enums;

namespace nem.Mimir.Application.Conversations.Commands;

public sealed record ConvertConversationToChannelCommand(
    Guid ConversationId,
    string? ChannelName) : ICommand<ConvertConversationToChannelDto>;

public sealed class ConvertConversationToChannelCommandValidator : AbstractValidator<ConvertConversationToChannelCommand>
{
    public ConvertConversationToChannelCommandValidator()
    {
        RuleFor(x => x.ConversationId)
            .NotEmpty().WithMessage("Conversation ID is required.");

        RuleFor(x => x.ChannelName)
            .MaximumLength(100).WithMessage("Channel name must not exceed 100 characters.");
    }
}

public sealed class ConvertConversationToChannelHandler
{
    public static async Task<ConvertConversationToChannelDto> Handle(
        ConvertConversationToChannelCommand command,
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

        var channelName = string.IsNullOrWhiteSpace(command.ChannelName)
            ? conversation.Title
            : command.ChannelName.Trim();

        var channel = Channel.Create(conversation.UserId, channelName, $"Converted from conversation {conversation.Id}", ChannelType.Public);
        channel.SetSourceConversation(conversation.Id);

        foreach (var message in conversation.Messages.OrderBy(message => message.CreatedAt))
        {
            var senderId = message.Role == MessageRole.User ? conversation.UserId : channel.OwnerId;
            channel.AddMessage(senderId, message.Content);
        }

        conversation.Archive();

        await channelRepository.CreateAsync(channel, ct).ConfigureAwait(false);
        await conversationRepository.UpdateAsync(conversation, ct).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);

        return new ConvertConversationToChannelDto(conversation.Id, channel.Id.Value, channel.Name);
    }
}
