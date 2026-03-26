using System.Text;
using FluentValidation;
using nem.Contracts.Events.Integration;
using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Mappings;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Domain.Enums;
using Wolverine;

namespace nem.Mimir.Application.Conversations.Commands;

public sealed record RegenerateMessageCommand(
    Guid ConversationId,
    Guid MessageId,
    string? Model) : ICommand<MessageDto>;

public sealed class RegenerateMessageCommandValidator : AbstractValidator<RegenerateMessageCommand>
{
    public RegenerateMessageCommandValidator()
    {
        RuleFor(x => x.ConversationId)
            .NotEmpty().WithMessage("Conversation ID is required.");

        RuleFor(x => x.MessageId)
            .NotEmpty().WithMessage("Message ID is required.");
    }
}

internal sealed class RegenerateMessageCommandHandler
{
    private const string DefaultModel = "phi-4-mini";

    private readonly IConversationRepository _repository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILlmService _llmService;
    private readonly IContextWindowService _contextWindowService;
    private readonly MimirMapper _mapper;
    private readonly IMessageBus _messageBus;

    public RegenerateMessageCommandHandler(
        IConversationRepository repository,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork,
        ILlmService llmService,
        IContextWindowService contextWindowService,
        MimirMapper mapper,
        IMessageBus messageBus)
    {
        _repository = repository;
        _currentUserService = currentUserService;
        _unitOfWork = unitOfWork;
        _llmService = llmService;
        _contextWindowService = contextWindowService;
        _mapper = mapper;
        _messageBus = messageBus;
    }

    public async Task<MessageDto> Handle(RegenerateMessageCommand command, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId
            ?? throw new ForbiddenAccessException("User is not authenticated.");

        if (!Guid.TryParse(userId, out var userGuid))
            throw new ForbiddenAccessException("User identity could not be determined.");

        var conversation = await _repository.GetWithMessagesAsync(command.ConversationId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(Domain.Entities.Conversation), command.ConversationId);

        if (conversation.UserId != userGuid)
            throw new ForbiddenAccessException();

        var sourceMessage = conversation.Messages.FirstOrDefault(message => message.Id == command.MessageId)
            ?? throw new NotFoundException(nameof(Domain.Entities.Message), command.MessageId);

        if (sourceMessage.Role != MessageRole.Assistant)
        {
            throw new nem.Mimir.Application.Common.Exceptions.ValidationException(new Dictionary<string, string[]>
            {
                [nameof(command.MessageId)] = ["Only assistant messages can be regenerated."],
            });
        }

        var rootMessageId = sourceMessage.ParentMessageId ?? sourceMessage.Id;
        var branchIndex = conversation.Messages
            .Where(message =>
                message.Role == MessageRole.Assistant &&
                (message.Id == rootMessageId || message.ParentMessageId == rootMessageId))
            .Select(message => message.BranchIndex)
            .DefaultIfEmpty(0)
            .Max() + 1;

        var latestUserMessage = conversation.Messages
            .Where(message => message.Role == MessageRole.User)
            .OrderByDescending(message => message.CreatedAt)
            .FirstOrDefault();

        var prompt = latestUserMessage?.Content ?? sourceMessage.Content;
        var model = command.Model ?? sourceMessage.Model ?? DefaultModel;

        var llmMessages = await _contextWindowService.BuildLlmMessagesAsync(conversation, prompt, model, cancellationToken)
            .ConfigureAwait(false);

        var responseBuilder = new StringBuilder();
        await foreach (var chunk in _llmService.StreamMessageAsync(model, llmMessages, cancellationToken).ConfigureAwait(false))
        {
            responseBuilder.Append(chunk.Content);
        }

        var regenerated = conversation.AddMessage(MessageRole.Assistant, responseBuilder.ToString(), model);
        regenerated.SetBranch(rootMessageId, branchIndex);
        regenerated.SetTokenCount(_contextWindowService.EstimateTokenCount(regenerated.Content));

        await _repository.UpdateAsync(conversation, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await _messageBus.PublishAsync(new MessageCreatedEvent(
            SessionId: Guid.Empty,
            MessageId: regenerated.Id,
            TenantId: _currentUserService.UserId ?? string.Empty,
            SenderName: _currentUserService.UserId ?? "Unknown",
            Text: regenerated.Content,
            Timestamp: DateTimeOffset.UtcNow,
            ChannelType: nem.Contracts.Channels.ChannelType.WebWidget));

        return _mapper.MapToMessageDto(regenerated);
    }
}
