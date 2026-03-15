namespace nem.Mimir.Tui.Models;

/// <summary>
/// Represents a single token streamed from the ChatHub.
/// Mirrors nem.Mimir.Api.Hubs.ChatToken for client-side deserialization.
/// </summary>
internal sealed record ChatTokenDto(
    string Token,
    bool IsComplete,
    int? QueuePosition);
