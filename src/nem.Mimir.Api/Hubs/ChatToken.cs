namespace nem.Mimir.Api.Hubs;

/// <summary>
/// Represents a single token (chunk) streamed from the LLM to the client via SignalR.
/// </summary>
/// <param name="Token">The text chunk from the LLM response.</param>
/// <param name="IsComplete">True when this is the final chunk in the stream.</param>
/// <param name="QueuePosition">The request's position in the LLM queue; null when actively processing.</param>
public sealed record ChatToken(
    string Token,
    bool IsComplete,
    int? QueuePosition);
