using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Channels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Sanitization;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Domain.Enums;
using nem.Mimir.Infrastructure.LiteLlm;

namespace nem.Mimir.Api.Hubs;

/// <summary>
/// SignalR hub for real-time LLM chat streaming.
/// Streams tokens from the LLM to the client as they are generated via IAsyncEnumerable.
/// </summary>
[Authorize]
public sealed class ChatHub : Hub
{
    private const string DefaultModel = "phi-4-mini";

    private readonly IConversationRepository _repository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILlmService _llmService;
    private readonly IContextWindowService _contextWindowService;
    private readonly LlmRequestQueue _requestQueue;
    private readonly ILogger<ChatHub> _logger;
    private readonly ISanitizationService _sanitizationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatHub"/> class.
    /// </summary>
    public ChatHub(
        IConversationRepository repository,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork,
        ILlmService llmService,
        IContextWindowService contextWindowService,
        LlmRequestQueue requestQueue,
        ILogger<ChatHub> logger,
        ISanitizationService sanitizationService)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(currentUserService);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(llmService);
        ArgumentNullException.ThrowIfNull(contextWindowService);
        ArgumentNullException.ThrowIfNull(requestQueue);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(sanitizationService);

        _repository = repository;
        _currentUserService = currentUserService;
        _unitOfWork = unitOfWork;
        _llmService = llmService;
        _contextWindowService = contextWindowService;
        _requestQueue = requestQueue;
        _logger = logger;
        _sanitizationService = sanitizationService;
    }

    /// <inheritdoc />
    public override async Task OnConnectedAsync()
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrWhiteSpace(userId))
        {
            Context.Abort();
            return;
        }

        _logger.LogInformation("User {UserId} connected to ChatHub. ConnectionId: {ConnectionId}",
            userId, Context.ConnectionId);

        await base.OnConnectedAsync();
    }

    /// <inheritdoc />
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (exception is not null)
        {
            _logger.LogWarning(exception, "User disconnected from ChatHub with error. ConnectionId: {ConnectionId}",
                Context.ConnectionId);
        }
        else
        {
            _logger.LogInformation("User disconnected from ChatHub. ConnectionId: {ConnectionId}",
                Context.ConnectionId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Streams a chat message response from the LLM as individual tokens.
    /// Uses a Channel internally to bridge streaming with yield-based IAsyncEnumerable
    /// (C# does not allow yield inside try-catch).
    /// </summary>
    /// <param name="conversationId">The conversation identifier.</param>
    /// <param name="content">The user's message content.</param>
    /// <param name="model">Optional LLM model identifier. Defaults to phi-4-mini.</param>
    /// <param name="cancellationToken">Cancellation token, triggered when the client disconnects.</param>
    /// <returns>An async enumerable of chat tokens streamed to the client.</returns>
    public async IAsyncEnumerable<ChatToken> SendMessage(
        string conversationId,
        string content,
        string? model,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId
            ?? throw new ForbiddenAccessException("User is not authenticated.");

        if (!Guid.TryParse(userId, out var userGuid))
            throw new ForbiddenAccessException("User identity could not be determined.");

        if (!Guid.TryParse(conversationId, out var conversationGuid))
        {
            yield return new ChatToken("Invalid conversation ID.", true, null);
            yield break;
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            yield return new ChatToken("Message content is required.", true, null);
            yield break;
        }

        // Sanitize user input before any processing
        content = _sanitizationService.SanitizeUserInput(content);

        // Load conversation and verify ownership BEFORE adding to group
        var conversation = await _repository.GetWithMessagesAsync(conversationGuid, cancellationToken);
        if (conversation is null || conversation.UserId != userGuid)
        {
            yield return new ChatToken("Conversation not found.", true, null);
            yield break;
        }

        // Add connection to conversation group only after ownership is verified
        await Groups.AddToGroupAsync(Context.ConnectionId, conversationId, cancellationToken);

        var selectedModel = model ?? DefaultModel;

        // Build LLM messages with context window management
        var llmMessages = await _contextWindowService.BuildLlmMessagesAsync(conversation, content, selectedModel, cancellationToken);

        // Persist user message
        conversation.AddMessage(MessageRole.User, content);
        await _repository.UpdateAsync(conversation, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Report queue position
        var queuePosition = _requestQueue.GetQueuePosition();
        if (queuePosition > 1)
        {
            yield return new ChatToken(string.Empty, false, queuePosition);
        }

        // Use a Channel to bridge the LLM streaming (which needs try-catch)
        // with the IAsyncEnumerable yield (which cannot be inside try-catch).
        var channel = Channel.CreateUnbounded<ChatToken>(new UnboundedChannelOptions
        {
            SingleWriter = true,
            SingleReader = true,
        });

        var responseBuilder = new StringBuilder();

        // Fire-and-forget the producer task that streams from LLM into the channel
        _ = ProduceTokensAsync(channel.Writer, conversation, selectedModel, llmMessages, responseBuilder, cancellationToken);

        // Yield tokens from the channel reader
        await foreach (var token in channel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return token;
        }
    }

    private async Task ProduceTokensAsync(
        ChannelWriter<ChatToken> writer,
        Domain.Entities.Conversation conversation,
        string model,
        IReadOnlyList<LlmMessage> llmMessages,
        StringBuilder responseBuilder,
        CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var chunk in _llmService.StreamMessageAsync(model, llmMessages, cancellationToken))
            {
                responseBuilder.Append(chunk.Content);

                var isComplete = chunk.FinishReason is not null;
                await writer.WriteAsync(new ChatToken(chunk.Content, isComplete, null), cancellationToken);
            }

            // Persist complete assistant message
            var fullResponse = responseBuilder.ToString();
            if (fullResponse.Length > 0)
            {
                var assistantMessage = conversation.AddMessage(MessageRole.Assistant, fullResponse, model);
                assistantMessage.SetTokenCount(_contextWindowService.EstimateTokenCount(fullResponse));
                await _repository.UpdateAsync(conversation, CancellationToken.None);
                await _unitOfWork.SaveChangesAsync(CancellationToken.None);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Client disconnected — persist partial response
            _logger.LogWarning("Client disconnected during streaming for conversation {ConversationId}",
                conversation.Id);
            await PersistPartialResponse(conversation, responseBuilder, model);
        }
        catch (Exception ex) // Intentional catch-all: streaming error boundary must catch all to send generic error to client and persist partial response
        {
            _logger.LogError(ex, "Error streaming LLM response for conversation {ConversationId}",
                conversation.Id);

            // Persist partial response on error
            await PersistPartialResponse(conversation, responseBuilder, model);

            // Write generic error token — never expose internal exception details to clients
            try
            {
                await writer.WriteAsync(new ChatToken("An error occurred while generating the response. Please try again.", true, null), CancellationToken.None);
            }
            catch
            {
                // Channel may already be closed — ignore
            }
        }
        finally
        {
            writer.TryComplete();
        }
    }

    private async Task PersistPartialResponse(
        Domain.Entities.Conversation conversation,
        StringBuilder responseBuilder,
        string model)
    {
        try
        {
            var partialResponse = responseBuilder.ToString();
            if (partialResponse.Length > 0)
            {
                var assistantMessage = conversation.AddMessage(MessageRole.Assistant, partialResponse, model);
                assistantMessage.SetTokenCount(_contextWindowService.EstimateTokenCount(partialResponse));

                // Use a fresh CancellationToken for persistence — the original may be cancelled
                await _repository.UpdateAsync(conversation, CancellationToken.None);
                await _unitOfWork.SaveChangesAsync(CancellationToken.None);

                _logger.LogInformation(
                    "Persisted partial assistant response ({Length} chars) for conversation {ConversationId}",
                    partialResponse.Length, conversation.Id);
            }
        }
        catch (Exception ex) // Intentional catch-all: persistence errors must not propagate, already in error-handling path
        {
            _logger.LogError(ex, "Failed to persist partial response for conversation {ConversationId}",
                conversation.Id);
        }
    }

}
