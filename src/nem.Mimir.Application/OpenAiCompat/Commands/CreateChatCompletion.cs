using System.Diagnostics;
using MediatR;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Application.OpenAiCompat.Models;

namespace nem.Mimir.Application.OpenAiCompat.Commands;

/// <summary>
/// Command to create a non-streaming chat completion via the LLM service.
/// </summary>
/// <param name="Model">The model identifier.</param>
/// <param name="Messages">The conversation messages.</param>
public sealed record CreateChatCompletionCommand(
    string Model,
    List<ChatMessageDto> Messages) : ICommand<ChatCompletionResultDto>;

/// <summary>
/// Handles <see cref="CreateChatCompletionCommand"/> by sending messages to the LLM service
/// and returning the completion result with timing metrics.
/// </summary>
internal sealed class CreateChatCompletionCommandHandler
    : IRequestHandler<CreateChatCompletionCommand, ChatCompletionResultDto>
{
    private readonly ILlmService _llmService;

    public CreateChatCompletionCommandHandler(ILlmService llmService)
    {
        _llmService = llmService;
    }

    public async Task<ChatCompletionResultDto> Handle(
        CreateChatCompletionCommand request,
        CancellationToken cancellationToken)
    {
        var llmMessages = request.Messages
            .Select(m => new LlmMessage(m.Role, m.Content))
            .ToList();

        var stopwatch = Stopwatch.StartNew();

        var llmResponse = await _llmService.SendMessageAsync(request.Model, llmMessages, cancellationToken);

        stopwatch.Stop();

        return new ChatCompletionResultDto
        {
            Content = llmResponse.Content,
            Model = llmResponse.Model,
            FinishReason = llmResponse.FinishReason ?? "stop",
            PromptTokens = llmResponse.PromptTokens,
            CompletionTokens = llmResponse.CompletionTokens,
            TotalTokens = llmResponse.TotalTokens,
            Duration = stopwatch.Elapsed,
        };
    }
}
