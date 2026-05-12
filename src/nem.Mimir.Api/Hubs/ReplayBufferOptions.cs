namespace nem.Mimir.Api.Hubs;

/// <summary>
/// Configuration options for the in-memory replay buffer.
/// </summary>
public sealed class ReplayBufferOptions
{
    /// <summary>
    /// Configuration section name for binding from appsettings.
    /// </summary>
    public const string SectionName = "ReplayBuffer";

    /// <summary>
    /// Maximum number of messages retained per conversation/user pair.
    /// </summary>
    public int MaxMessages { get; set; } = 50;

    /// <summary>
    /// Replay window length in minutes.
    /// </summary>
    public int WindowMinutes { get; set; } = 5;
}
