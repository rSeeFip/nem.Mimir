using System.Collections.Concurrent;

namespace Mimir.Telegram.Services;

/// <summary>
/// Tracks the current conversation and selected model per Telegram user.
/// </summary>
internal sealed class UserStateManager
{
    private readonly ConcurrentDictionary<long, UserState> _states = new();

    /// <summary>
    /// Gets or creates a state entry for the given Telegram user.
    /// </summary>
    public UserState GetOrCreateState(long telegramUserId)
    {
        return _states.GetOrAdd(telegramUserId, _ => new UserState());
    }

    /// <summary>
    /// Gets the state for a Telegram user if it exists.
    /// </summary>
    public UserState? GetState(long telegramUserId)
    {
        return _states.TryGetValue(telegramUserId, out var state) ? state : null;
    }

    /// <summary>
    /// Sets the current conversation for a Telegram user.
    /// </summary>
    public void SetCurrentConversation(long telegramUserId, Guid conversationId, string title)
    {
        var state = GetOrCreateState(telegramUserId);
        state.CurrentConversationId = conversationId;
        state.CurrentConversationTitle = title;
    }

    /// <summary>
    /// Sets the selected model for a Telegram user.
    /// </summary>
    public void SetSelectedModel(long telegramUserId, string modelId)
    {
        var state = GetOrCreateState(telegramUserId);
        state.SelectedModel = modelId;
    }

    /// <summary>
    /// Marks a Telegram user as authenticated with the given bearer token.
    /// </summary>
    public void SetAuthenticated(long telegramUserId, string bearerToken)
    {
        var state = GetOrCreateState(telegramUserId);
        state.BearerToken = bearerToken;
        state.IsAuthenticated = true;
    }

    /// <summary>
    /// Checks if a Telegram user is authenticated.
    /// </summary>
    public bool IsAuthenticated(long telegramUserId)
    {
        var state = GetState(telegramUserId);
        return state is { IsAuthenticated: true, BearerToken: not null };
    }
}

/// <summary>
/// Represents the state of a Telegram user's session.
/// </summary>
internal sealed class UserState
{
    public Guid? CurrentConversationId { get; set; }
    public string? CurrentConversationTitle { get; set; }
    public string? SelectedModel { get; set; }
    public string? BearerToken { get; set; }
    public bool IsAuthenticated { get; set; }
}
