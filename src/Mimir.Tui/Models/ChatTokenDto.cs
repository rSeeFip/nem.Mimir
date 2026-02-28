namespace Mimir.Tui.Models;

/// <summary>
/// Represents a single token streamed from the ChatHub.
/// Mirrors Mimir.Api.Hubs.ChatToken for client-side deserialization.
/// </summary>
internal sealed record ChatTokenDto(
    string Token,
    bool IsComplete,
    int? QueuePosition);
