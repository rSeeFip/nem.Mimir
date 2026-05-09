using System.Text;
using nem.Mimir.Application.Common.Mappings;
using FluentValidation;
using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Application.Conversations.Services;
using nem.Mimir.Application.Knowledge;
using nem.Mimir.Domain.Enums;
using nem.Mimir.Domain.ValueObjects;
using nem.Contracts.Events.Integration;
using Wolverine;

namespace nem.Mimir.Application.Conversations.Commands;

/// <summary>
/// Command to send a user message to a conversation and receive an LLM-generated response.
/// </summary>
/// <param name="ConversationId">The conversation to send the message to.</param>
/// <param name="Content">The message content.</param>
/// <param name="Model">An optional LLM model identifier to use; defaults to the system default if not specified.</param>
public sealed record SendMessageCommand(
    Guid ConversationId,
    string Content,
    string? Model) : ICommand<MessageDto>;

/// <summary>
/// Validates the <see cref="SendMessageCommand"/> ensuring the conversation ID and content are valid.
/// </summary>
public sealed class SendMessageCommandValidator : AbstractValidator<SendMessageCommand>
{
    public SendMessageCommandValidator()
    {
        RuleFor(x => x.ConversationId)
            .NotEmpty().WithMessage("Conversation ID is required.");

        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("Message content is required.")
            .MaximumLength(32_000).WithMessage("Message content must not exceed 32000 characters.");
    }
}

internal sealed class SendMessageCommandHandler
{
    private const string DefaultModel = "phi-4-mini";

    private readonly IConversationRepository _repository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILlmService _llmService;
    private readonly IContextWindowService _contextWindowService;
    private readonly IConversationContextService _conversationRagService;
    private readonly MimirMapper _mapper;
    private readonly IMessageBus _messageBus;

    public SendMessageCommandHandler(
        IConversationRepository repository,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork,
        ILlmService llmService,
        IContextWindowService contextWindowService,
        IConversationContextService conversationRagService,
        MimirMapper mapper,
        IMessageBus messageBus)
    {
        _repository = repository;
        _currentUserService = currentUserService;
        _unitOfWork = unitOfWork;
        _llmService = llmService;
        _contextWindowService = contextWindowService;
        _conversationRagService = conversationRagService;
        _mapper = mapper;
        _messageBus = messageBus;
    }

    public async Task<MessageDto> Handle(SendMessageCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId
            ?? throw new ForbiddenAccessException("User is not authenticated.");

        var userGuid = Guid.Parse(userId);

        var conversation = await _repository.GetWithMessagesAsync(request.ConversationId, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.Conversation), request.ConversationId);

        if (conversation.UserId != userGuid)
            throw new NotFoundException(nameof(Domain.Entities.Conversation), request.ConversationId);

        var model = request.Model ?? DefaultModel;
        var ragSources = await _conversationRagService
            .GetRagContextAsync(request.ConversationId, request.Content, cancellationToken)
            .ConfigureAwait(false);

        var ragContextPrefix = string.Empty;
        if (ragSources.Count > 0)
        {
            var serializedContext = string.Join("\n", ragSources.Select(source => $"[{source.DocumentId}] {source.ChunkText}"));
            ragContextPrefix = $"Use the following retrieved context when answering:\n{serializedContext}\n\n";
        }

        // Build LLM messages with context window management (before adding to entity)
        var llmMessages = await _contextWindowService
            .BuildLlmMessagesAsync(conversation, $"{ragContextPrefix}{request.Content}", model, cancellationToken)
            .ConfigureAwait(false);

        // Add user message to conversation (persisted later)
        conversation.AddMessage(MessageRole.User, request.Content);
        // Stream the LLM response
        var responseBuilder = new StringBuilder();
        await foreach (var chunk in _llmService.StreamMessageAsync(model, llmMessages, cancellationToken))
        {
            responseBuilder.Append(chunk.Content);
        }

        var fullResponse = AnswerSourcesFormatter.AppendSources(responseBuilder.ToString(), ragSources);

        // Add assistant message to conversation
        var assistantMessage = conversation.AddMessage(MessageRole.Assistant, fullResponse, model);
        assistantMessage.SetKnowledgeSources(ragSources.Select(source => new MessageKnowledgeSource(
            source.DocumentId,
            source.ChunkText,
            source.Similarity,
            source.EntityType,
            source.EntityId)));

        // Estimate and set token count on assistant message
        var tokenCount = _contextWindowService.EstimateTokenCount(fullResponse);
        assistantMessage.SetTokenCount(tokenCount);

        // Persist changes
        await _repository.UpdateAsync(conversation, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Publish message creation event for nem.Comms operator inbox projection
        await _messageBus.PublishAsync(new MessageCreatedEvent(
            SessionId: Guid.Empty,
            MessageId: Guid.NewGuid(),
            TenantId: _currentUserService.UserId ?? string.Empty,
            SenderName: _currentUserService.UserId ?? "Unknown",
            Text: request.Content,
            Timestamp: DateTimeOffset.UtcNow,
            ChannelType: nem.Contracts.Channels.ChannelType.WebWidget));

        return _mapper.MapToMessageDto(assistantMessage);
    }
}
