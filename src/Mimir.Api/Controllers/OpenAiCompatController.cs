using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mimir.Api.Models.OpenAi;
using Mimir.Application.Common.Interfaces;
using Mimir.Application.Common.Models;
using Mimir.Sync.Messages;
using Mimir.Sync.Publishers;

namespace Mimir.Api.Controllers;

/// <summary>
/// Provides OpenAI-compatible API endpoints for chat completions and model listing.
/// This allows clients that speak the OpenAI protocol (e.g., openchat-ui) to use
/// Mimir as a drop-in backend.
/// </summary>
[ApiController]
[Route("v1")]
[Authorize]
public sealed class OpenAiCompatController : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly ILlmService _llmService;
    private readonly IMimirEventPublisher _eventPublisher;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<OpenAiCompatController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenAiCompatController"/> class.
    /// </summary>
    /// <param name="llmService">Service for LLM operations.</param>
    /// <param name="eventPublisher">Publisher for Wolverine events.</param>
    /// <param name="currentUserService">Service to retrieve the current authenticated user.</param>
    /// <param name="logger">Logger instance.</param>
    public OpenAiCompatController(
        ILlmService llmService,
        IMimirEventPublisher eventPublisher,
        ICurrentUserService currentUserService,
        ILogger<OpenAiCompatController> logger)
    {
        _llmService = llmService;
        _eventPublisher = eventPublisher;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    /// <summary>
    /// Creates a chat completion. Supports both streaming (SSE) and non-streaming responses,
    /// following the OpenAI API format.
    /// </summary>
    /// <param name="request">The OpenAI-compatible chat completion request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpPost("chat/completions")]
    [ProducesResponseType(typeof(ChatCompletionResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task ChatCompletions(
        [FromBody] ChatCompletionRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Messages is not { Count: > 0 })
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            await Response.WriteAsJsonAsync(
                new { error = new { message = "messages must be a non-empty array", type = "invalid_request_error" } },
                cancellationToken);
            return;
        }

        var completionId = $"chatcmpl-{Guid.NewGuid():N}";
        var created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var userId = GetUserId();
        var conversationId = Guid.NewGuid();

        var messages = request.Messages
            .Select(m => new LlmMessage(m.Role, m.Content))
            .ToList();

        var userContent = request.Messages.LastOrDefault(m =>
            string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase))?.Content ?? string.Empty;

        // Publish chat request received
        await _eventPublisher.PublishChatRequestAsync(
            new ChatRequestReceived(conversationId, userId, request.Model, userContent, DateTimeOffset.UtcNow),
            cancellationToken);

        if (request.Stream)
        {
            await HandleStreamingResponse(request, messages, completionId, created, conversationId, userId, cancellationToken);
        }
        else
        {
            await HandleNonStreamingResponse(request, messages, completionId, created, conversationId, cancellationToken);
        }
    }

    /// <summary>
    /// Lists available models in OpenAI-compatible format.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The list of available models.</returns>
    [HttpGet("models")]
    [ProducesResponseType(typeof(OpenAiModelsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ListModels(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Listing models in OpenAI-compatible format");

        var models = await _llmService.GetAvailableModelsAsync(cancellationToken);

        var response = new OpenAiModelsResponse
        {
            Data = models
                .Where(m => m.IsAvailable)
                .Select(m => new OpenAiModel
                {
                    Id = m.Id,
                    Created = 0,
                    OwnedBy = "mimir",
                })
                .ToList(),
        };

        return Ok(response);
    }

    private async Task HandleStreamingResponse(
        ChatCompletionRequest request,
        List<LlmMessage> messages,
        string completionId,
        long created,
        Guid conversationId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";
        Response.StatusCode = StatusCodes.Status200OK;

        // Force headers to be sent immediately
        await Response.StartAsync(cancellationToken);

        var stopwatch = Stopwatch.StartNew();
        var model = request.Model;
        var isFirstChunk = true;
        var completionTokens = 0;

        try
        {
            await foreach (var chunk in _llmService.StreamMessageAsync(request.Model, messages, cancellationToken))
            {
                if (!string.IsNullOrEmpty(chunk.Model))
                {
                    model = chunk.Model;
                }

                var sseChunk = new ChatCompletionChunk
                {
                    Id = completionId,
                    Created = created,
                    Model = model,
                    Choices =
                    [
                        new ChunkChoice
                        {
                            Index = 0,
                            Delta = new ChunkDelta
                            {
                                Role = isFirstChunk ? "assistant" : null,
                                Content = chunk.Content,
                            },
                            FinishReason = chunk.FinishReason,
                        },
                    ],
                };

                isFirstChunk = false;
                completionTokens++;

                var json = JsonSerializer.Serialize(sseChunk, JsonOptions);
                await Response.WriteAsync($"data: {json}\n\n", cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
            }

            // Send the final [DONE] marker
            await Response.WriteAsync("data: [DONE]\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);

            stopwatch.Stop();

            // Publish chat completed event
            await _eventPublisher.PublishChatCompletedAsync(
                new ChatCompleted(
                    conversationId,
                    Guid.NewGuid(),
                    model,
                    PromptTokens: EstimatePromptTokens(messages),
                    CompletionTokens: completionTokens,
                    Duration: stopwatch.Elapsed,
                    Timestamp: DateTimeOffset.UtcNow),
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Streaming chat completion was cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during streaming chat completion");
        }
    }

    private async Task HandleNonStreamingResponse(
        ChatCompletionRequest request,
        List<LlmMessage> messages,
        string completionId,
        long created,
        Guid conversationId,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        var llmResponse = await _llmService.SendMessageAsync(request.Model, messages, cancellationToken);

        stopwatch.Stop();

        var result = new ChatCompletionResult
        {
            Id = completionId,
            Created = created,
            Model = llmResponse.Model,
            Choices =
            [
                new ResultChoice
                {
                    Index = 0,
                    Message = new ResultMessage
                    {
                        Content = llmResponse.Content,
                    },
                    FinishReason = llmResponse.FinishReason ?? "stop",
                },
            ],
            Usage = new UsageInfo
            {
                PromptTokens = llmResponse.PromptTokens,
                CompletionTokens = llmResponse.CompletionTokens,
                TotalTokens = llmResponse.TotalTokens,
            },
        };

        // Publish chat completed event
        await _eventPublisher.PublishChatCompletedAsync(
            new ChatCompleted(
                conversationId,
                Guid.NewGuid(),
                llmResponse.Model,
                PromptTokens: llmResponse.PromptTokens,
                CompletionTokens: llmResponse.CompletionTokens,
                Duration: stopwatch.Elapsed,
                Timestamp: DateTimeOffset.UtcNow),
            cancellationToken);

        Response.ContentType = "application/json";
        await Response.WriteAsJsonAsync(result, JsonOptions, cancellationToken);
    }

    private Guid GetUserId()
    {
        if (_currentUserService.UserId is not null && Guid.TryParse(_currentUserService.UserId, out var userId))
        {
            return userId;
        }

        return Guid.Empty;
    }

    private static int EstimatePromptTokens(List<LlmMessage> messages)
    {
        // Rough estimation: ~4 characters per token
        return messages.Sum(m => (m.Content.Length + m.Role.Length) / 4);
    }
}
