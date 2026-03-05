using System.Text;
using Mimir.Application.Common.Mappings;
using FluentValidation;
using MediatR;
using Mimir.Application.Common.Exceptions;
using Mimir.Application.Common.Interfaces;
using Mimir.Application.Common.Models;
using Mimir.Domain.Enums;
using Mimir.Domain.Tools;

namespace Mimir.Application.Conversations.Commands;

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

internal sealed class SendMessageCommandHandler : IRequestHandler<SendMessageCommand, MessageDto>
{
    private const string DefaultModel = "phi-4-mini";
    private const int MaxToolIterations = 5;

    private readonly IConversationRepository _repository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILlmService _llmService;
    private readonly IContextWindowService _contextWindowService;
    private readonly MimirMapper _mapper;
    private readonly IToolProvider _toolProvider;

    public SendMessageCommandHandler(
        IConversationRepository repository,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork,
        ILlmService llmService,
        IContextWindowService contextWindowService,
        MimirMapper mapper,
        IToolProvider toolProvider)
    {
        _repository = repository;
        _currentUserService = currentUserService;
        _unitOfWork = unitOfWork;
        _llmService = llmService;
        _contextWindowService = contextWindowService;
        _mapper = mapper;
        _toolProvider = toolProvider;
    }

    public async Task<MessageDto> Handle(SendMessageCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId
            ?? throw new ForbiddenAccessException("User is not authenticated.");

        if (!Guid.TryParse(userId, out var userGuid))
            throw new ForbiddenAccessException("User identity could not be determined.");

        var conversation = await _repository.GetWithMessagesAsync(request.ConversationId, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.Conversation), request.ConversationId);

        if (conversation.UserId != userGuid)
            throw new NotFoundException(nameof(Domain.Entities.Conversation), request.ConversationId);

        var model = request.Model ?? DefaultModel;

        var llmMessages = await _contextWindowService.BuildLlmMessagesAsync(conversation, request.Content, model, cancellationToken);

        conversation.AddMessage(MessageRole.User, request.Content);

        var tools = await _toolProvider.GetAvailableToolsAsync(cancellationToken);

        string fullResponse;
        if (tools.Count == 0)
        {
            fullResponse = await StreamResponseAsync(model, llmMessages, cancellationToken);
        }
        else
        {
            fullResponse = await ExecuteToolLoopAsync(model, llmMessages, tools, cancellationToken);
        }

        var assistantMessage = conversation.AddMessage(MessageRole.Assistant, fullResponse, model);

        var tokenCount = _contextWindowService.EstimateTokenCount(fullResponse);
        assistantMessage.SetTokenCount(tokenCount);

        await _repository.UpdateAsync(conversation, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.MapToMessageDto(assistantMessage);
    }

    private async Task<string> StreamResponseAsync(
        string model,
        IReadOnlyList<LlmMessage> llmMessages,
        CancellationToken cancellationToken)
    {
        var responseBuilder = new StringBuilder();
        await foreach (var chunk in _llmService.StreamMessageAsync(model, llmMessages, cancellationToken))
        {
            responseBuilder.Append(chunk.Content);
        }

        return responseBuilder.ToString();
    }

    private async Task<string> ExecuteToolLoopAsync(
        string model,
        IReadOnlyList<LlmMessage> llmMessages,
        IReadOnlyList<ToolDefinition> tools,
        CancellationToken cancellationToken)
    {
        var messages = new List<LlmMessage>(llmMessages);

        for (var iteration = 0; iteration < MaxToolIterations; iteration++)
        {
            var response = await _llmService.SendMessageAsync(model, messages, tools, cancellationToken);

            if (response.ToolCalls is not { Count: > 0 })
            {
                return response.Content;
            }

            // Append the assistant message with tool calls
            messages.Add(new LlmMessage("assistant", response.Content ?? string.Empty)
            {
                ToolCalls = response.ToolCalls,
            });

            // Execute each tool call sequentially
            foreach (var toolCall in response.ToolCalls)
            {
                string toolContent;
                try
                {
                    var result = await _toolProvider.ExecuteToolAsync(toolCall.FunctionName, toolCall.ArgumentsJson, cancellationToken);
                    toolContent = result.IsSuccess
                        ? result.Content
                        : $"Error: {result.ErrorMessage}";
                }
                catch (Exception ex)
                {
                    toolContent = $"Error executing tool '{toolCall.FunctionName}': {ex.Message}";
                }

                messages.Add(new LlmMessage("tool", toolContent)
                {
                    ToolCallId = toolCall.Id,
                    Name = toolCall.FunctionName,
                });
            }
        }

        // Max iterations reached — make one final call without tools to get a text response
        var finalResponse = await _llmService.SendMessageAsync(model, messages, cancellationToken);
        return finalResponse.Content;
    }
}
