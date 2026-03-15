namespace nem.Mimir.Application.Common.Interfaces;

/// <summary>
/// Service interface for abstracting date and time operations.
/// Enables deterministic testing of time-dependent logic.
/// </summary>
public interface IDateTimeService
{
    /// <summary>
    /// Gets the current UTC date and time.
    /// </summary>
    DateTimeOffset UtcNow { get; }
}
