using Microsoft.Extensions.Logging;
using nem.Cognitive.Agents.Models;

namespace Mimir.Application.Agents;

/// <summary>
/// Represents a stored conversation turn for the agent memory.
/// </summary>
public sealed record ConversationTurn(
    string Role,
    string Content,
    DateTimeOffset Timestamp);

/// <summary>
/// Represents a conversation session aggregate stored in Marten.
/// </summary>
public sealed class ConversationSession
{
    /// <summary>The unique session identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The user who owns this session.</summary>
    public string? UserId { get; set; }

    /// <summary>Ordered list of conversation turns.</summary>
    public List<ConversationTurn> Messages { get; set; } = [];

    /// <summary>When this session was created.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>When this session was last active.</summary>
    public DateTimeOffset LastActiveAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Adds a user turn to the session.
    /// </summary>
    public void AddUserMessage(string content)
    {
        Messages.Add(new ConversationTurn("user", content, DateTimeOffset.UtcNow));
        LastActiveAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Adds an assistant turn to the session.
    /// </summary>
    public void AddAssistantMessage(string content)
    {
        Messages.Add(new ConversationTurn("assistant", content, DateTimeOffset.UtcNow));
        LastActiveAt = DateTimeOffset.UtcNow;
    }
}

/// <summary>
/// Provides conversation memory persistence for the Mimir Reasoner Agent.
/// Stores and retrieves conversation turns, converting them to
/// <see cref="ConversationMessage"/> for agent context injection.
/// </summary>
/// <remarks>
/// Uses an in-memory dictionary as a lightweight persistence layer.
/// In production this would be backed by Marten document storage.
/// </remarks>
public sealed class ConversationMemoryService
{
    private readonly ILogger<ConversationMemoryService> _logger;
    private readonly int _contextWindow;

    // In-memory store — Marten integration replaces this in production.
    private readonly Dictionary<string, ConversationSession> _sessions = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ConversationMemoryService"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="contextWindow">Maximum number of messages to include in context. Defaults to 20.</param>
    public ConversationMemoryService(ILogger<ConversationMemoryService> logger, int contextWindow = 20)
    {
        ArgumentNullException.ThrowIfNull(logger);

        _logger = logger;
        _contextWindow = contextWindow;
    }

    /// <summary>
    /// Gets or creates a conversation session for the given conversation ID.
    /// </summary>
    /// <param name="conversationId">The conversation identifier.</param>
    /// <param name="userId">Optional user identifier for the session.</param>
    /// <returns>The conversation session.</returns>
    public ConversationSession GetOrCreateSession(string conversationId, string? userId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);

        lock (_lock)
        {
            return GetOrCreateSessionUnsafe(conversationId, userId);
        }
    }

    /// <summary>
    /// Internal implementation of get-or-create that assumes the caller already holds <see cref="_lock"/>.
    /// </summary>
    private ConversationSession GetOrCreateSessionUnsafe(string conversationId, string? userId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);

        if (_sessions.TryGetValue(conversationId, out var existing))
        {
            return existing;
        }

        var session = new ConversationSession
        {
            Id = Guid.TryParse(conversationId, out var parsed) ? parsed : Guid.NewGuid(),
            UserId = userId,
        };

        _sessions[conversationId] = session;

        _logger.LogDebug("Created new conversation session {ConversationId} for user {UserId}",
            conversationId, userId);

        return session;
    }

    /// <summary>
    /// Adds a user message to the conversation and returns the updated session.
    /// </summary>
    /// <remarks>
    /// BIZ-LOGIC: The entire get-or-create + add-message operation is performed under a
    /// single lock to prevent race conditions when concurrent callers target the same
    /// conversationId. Without this, two threads could both retrieve the same session and
    /// mutate its Messages list simultaneously (List&lt;T&gt; is not thread-safe).
    /// </remarks>
    public ConversationSession AddUserMessage(string conversationId, string content, string? userId = null)
    {
        lock (_lock)
        {
            var session = GetOrCreateSessionUnsafe(conversationId, userId);
            session.AddUserMessage(content);

            _logger.LogDebug("Added user message to session {ConversationId}. Total messages: {Count}",
                conversationId, session.Messages.Count);

            return session;
        }
    }

    /// <summary>
    /// Adds an assistant response to the conversation.
    /// </summary>
    /// <remarks>
    /// BIZ-LOGIC: Same lock semantics as <see cref="AddUserMessage"/> — see remarks there.
    /// </remarks>
    public ConversationSession AddAssistantMessage(string conversationId, string content)
    {
        lock (_lock)
        {
            var session = GetOrCreateSessionUnsafe(conversationId);
            session.AddAssistantMessage(content);

            _logger.LogDebug("Added assistant message to session {ConversationId}. Total messages: {Count}",
                conversationId, session.Messages.Count);

            return session;
        }
    }

    /// <summary>
    /// Builds the conversation history for agent context, limited to the configured context window.
    /// Returns the most recent N messages as <see cref="ConversationMessage"/> instances.
    /// </summary>
    /// <param name="conversationId">The conversation identifier.</param>
    /// <returns>A read-only list of conversation messages for the agent context.</returns>
    public IReadOnlyList<ConversationMessage> GetConversationHistory(string conversationId)
    {
        lock (_lock)
        {
            if (!_sessions.TryGetValue(conversationId, out var session))
            {
                return [];
            }

            var messages = session.Messages;
            var skip = Math.Max(0, messages.Count - _contextWindow);

            return messages
                .Skip(skip)
                .Select(m => new ConversationMessage(m.Role, m.Content))
                .ToList()
                .AsReadOnly();
        }
    }

    /// <summary>
    /// Gets the total number of messages in a conversation session.
    /// </summary>
    public int GetMessageCount(string conversationId)
    {
        lock (_lock)
        {
            return _sessions.TryGetValue(conversationId, out var session) ? session.Messages.Count : 0;
        }
    }

    /// <summary>
    /// Checks whether a session exists for the given conversation ID.
    /// </summary>
    public bool SessionExists(string conversationId)
    {
        lock (_lock)
        {
            return _sessions.ContainsKey(conversationId);
        }
    }

    /// <summary>
    /// Gets the configured context window size.
    /// </summary>
    public int ContextWindow => _contextWindow;
}
