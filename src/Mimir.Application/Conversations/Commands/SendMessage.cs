using System.Text;
using AutoMapper;
using FluentValidation;
using MediatR;
using Mimir.Application.Common.Exceptions;
using Mimir.Application.Common.Interfaces;
using Mimir.Application.Common.Models;
using Mimir.Domain.Enums;

namespace Mimir.Application.Conversations.Commands;

public sealed record SendMessageCommand(
    Guid ConversationId,
    string Content,
    string? Model) : ICommand<MessageDto>;

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

internal sealed class SendMessageCommandHandler : IRequestHandler<SendMessageCommand, MessageDto>
{
    private static readonly Dictionary<string, int> ModelTokenLimits = new(StringComparer.OrdinalIgnoreCase)
    {
        ["phi-4-mini"] = 16_384,
        ["qwen-2.5-72b"] = 131_072,
        ["qwen-2.5-coder-32b"] = 131_072,
    };

    private const int DefaultTokenLimit = 16_384;
    private const string DefaultModel = "phi-4-mini";
    private const string SystemPrompt = "You are Mimir, a helpful AI assistant.";

    private readonly IConversationRepository _repository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILlmService _llmService;
    private readonly IMapper _mapper;

    public SendMessageCommandHandler(
        IConversationRepository repository,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork,
        ILlmService llmService,
        IMapper mapper)
    {
        _repository = repository;
        _currentUserService = currentUserService;
        _unitOfWork = unitOfWork;
        _llmService = llmService;
        _mapper = mapper;
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
        var tokenLimit = GetTokenLimit(model);

        // Build LLM messages with context window management (before adding to entity)
        var llmMessages = BuildLlmMessages(conversation, request.Content, tokenLimit);

        // Add user message to conversation (persisted later)
        conversation.AddMessage(MessageRole.User, request.Content);
        // Stream the LLM response
        var responseBuilder = new StringBuilder();
        await foreach (var chunk in _llmService.StreamMessageAsync(model, llmMessages, cancellationToken))
        {
            responseBuilder.Append(chunk.Content);
        }

        var fullResponse = responseBuilder.ToString();

        // Add assistant message to conversation
        var assistantMessage = conversation.AddMessage(MessageRole.Assistant, fullResponse, model);

        // Estimate and set token count on assistant message
        var tokenCount = EstimateTokenCount(fullResponse);
        assistantMessage.SetTokenCount(tokenCount);

        // Persist changes
        await _repository.UpdateAsync(conversation, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<MessageDto>(assistantMessage);
    }

    internal static List<LlmMessage> BuildLlmMessages(
        Domain.Entities.Conversation conversation,
        string newUserContent,
        int tokenLimit)
    {
        var systemMessage = new LlmMessage("system", SystemPrompt);
        var newUserMessage = new LlmMessage("user", newUserContent);

        // Map existing history messages (ordered by creation time)
        var historyMessages = conversation.Messages
            .OrderBy(m => m.CreatedAt)
            .Select(m => new LlmMessage(m.Role.ToString().ToLowerInvariant(), m.Content))
            .ToList();

        // Calculate base token usage: system prompt + new user message
        var systemTokens = EstimateTokenCount(systemMessage.Content);
        var newUserTokens = EstimateTokenCount(newUserMessage.Content);
        var baseTokens = systemTokens + newUserTokens;

        // Calculate total history tokens
        var historyTokens = historyMessages
            .Select(m => EstimateTokenCount(m.Content))
            .ToList();

        var totalTokens = baseTokens + historyTokens.Sum();

        // Remove oldest non-system messages (from the front) until within limit
        var startIndex = 0;
        while (totalTokens > tokenLimit && startIndex < historyMessages.Count)
        {
            totalTokens -= historyTokens[startIndex];
            startIndex++;
        }

        // Build final message list
        var result = new List<LlmMessage> { systemMessage };
        result.AddRange(historyMessages.Skip(startIndex));
        result.Add(newUserMessage);

        return result;
    }

    private static int GetTokenLimit(string model) =>
        ModelTokenLimits.TryGetValue(model, out var limit) ? limit : DefaultTokenLimit;

    internal static int EstimateTokenCount(string text) =>
        string.IsNullOrEmpty(text) ? 0 : (text.Length + 3) / 4;
}
