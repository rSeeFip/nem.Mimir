using System.Diagnostics;
using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using nem.Mimir.Api.Models.OpenAi;
using nem.Mimir.Application.Common;
using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Application.OpenAiCompat.Commands;
using nem.Mimir.Application.OpenAiCompat.Models;
using nem.Mimir.Application.OpenAiCompat.Queries;
using nem.Mimir.Sync.Messages;
using nem.Mimir.Sync.Publishers;

namespace nem.Mimir.Api.Controllers;

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
    private readonly ISender _sender;
    private readonly ILlmService _llmService;
    private readonly IMimirEventPublisher _eventPublisher;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<OpenAiCompatController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenAiCompatController"/> class.
    /// </summary>
    /// <param name="sender">MediatR sender for dispatching queries and commands.</param>
    /// <param name="llmService">Service for LLM operations (used by the streaming path).</param>
    /// <param name="eventPublisher">Publisher for Wolverine events.</param>
    /// <param name="currentUserService">Service to retrieve the current authenticated user.</param>
    /// <param name="logger">Logger instance.</param>
    public OpenAiCompatController(
        ISender sender,
        ILlmService llmService,
        IMimirEventPublisher eventPublisher,
        ICurrentUserService currentUserService,
        ILogger<OpenAiCompatController> logger)
    {
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(llmService);
        ArgumentNullException.ThrowIfNull(eventPublisher);
        ArgumentNullException.ThrowIfNull(currentUserService);
        ArgumentNullException.ThrowIfNull(logger);

        _sender = sender;
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

        var userContent = request.Messages.LastOrDefault(m =>
            string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase))?.Content ?? string.Empty;

        // Publish chat request received
        await _eventPublisher.PublishChatRequestAsync(
            new ChatRequestReceived(conversationId, userId, request.Model, userContent, DateTimeOffset.UtcNow),
            cancellationToken);

        if (request.Stream)
        {
            var messages = request.Messages
                .Select(m => new LlmMessage(m.Role, m.Content))
                .ToList();

            await HandleStreamingResponse(request, messages, completionId, created, conversationId, userId, cancellationToken);
        }
        else
        {
            await HandleNonStreamingResponse(request, completionId, created, conversationId, cancellationToken);
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

        var models = await _sender.Send(new ListOpenAiModelsQuery(), cancellationToken);

        var response = new OpenAiModelsResponse
        {
            Data = models
                .Select(m => new OpenAiModel
                {
                    Id = m.Id,
                    Created = 0,
                    OwnedBy = m.OwnedBy,
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

                var json = JsonSerializer.Serialize(sseChunk, JsonDefaults.Options);
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
        catch (Exception ex) // Intentional catch-all: SSE streaming error boundary; any unhandled exception must be silently absorbed to avoid SSE protocol corruption
        {
            _logger.LogError(ex, "Error during streaming chat completion");
        }
    }

    private async Task HandleNonStreamingResponse(
        ChatCompletionRequest request,
        string completionId,
        long created,
        Guid conversationId,
        CancellationToken cancellationToken)
    {
        var command = new CreateChatCompletionCommand(
            request.Model,
            request.Messages.Select(m => new ChatMessageDto(m.Role, m.Content)).ToList());

        var dto = await _sender.Send(command, cancellationToken);

        var result = new ChatCompletionResult
        {
            Id = completionId,
            Created = created,
            Model = dto.Model,
            Choices =
            [
                new ResultChoice
                {
                    Index = 0,
                    Message = new ResultMessage
                    {
                        Content = dto.Content,
                    },
                    FinishReason = dto.FinishReason,
                },
            ],
            Usage = new UsageInfo
            {
                PromptTokens = dto.PromptTokens,
                CompletionTokens = dto.CompletionTokens,
                TotalTokens = dto.TotalTokens,
            },
        };

        // Publish chat completed event
        await _eventPublisher.PublishChatCompletedAsync(
            new ChatCompleted(
                conversationId,
                Guid.NewGuid(),
                dto.Model,
                PromptTokens: dto.PromptTokens,
                CompletionTokens: dto.CompletionTokens,
                Duration: dto.Duration,
                Timestamp: DateTimeOffset.UtcNow),
            cancellationToken);

        Response.ContentType = "application/json";
        await Response.WriteAsJsonAsync(result, JsonDefaults.Options, cancellationToken);
    }

    private Guid GetUserId()
    {
        if (_currentUserService.UserId is not null && Guid.TryParse(_currentUserService.UserId, out var userId))
        {
            return userId;
        }

        _logger.LogWarning("Failed to parse user ID from claims. UserId claim: {UserId}", _currentUserService.UserId);
        throw new ForbiddenAccessException("User identity could not be determined.");
    }

    private static int EstimatePromptTokens(List<LlmMessage> messages)
    {
        // BIZ-LOGIC: Rough estimation at ~4 characters per token. Used only for the
        // streaming path where exact token counts aren't available from LiteLLM.
        // Null-safe: LlmMessage Content/Role may be null when mapped from external input.
        return messages.Sum(m => ((m.Content?.Length ?? 0) + (m.Role?.Length ?? 0)) / 4);
    }
}
