namespace Mimir.Application.Common.Sanitization;

/// <summary>
/// Configuration settings for the sanitization service.
/// Bound from the "Sanitization" configuration section.
/// </summary>
public sealed class SanitizationSettings
{
    /// <summary>
    /// Configuration section name for IOptions binding.
    /// </summary>
    public const string SectionName = "Sanitization";

    /// <summary>
    /// Maximum allowed length for user input messages. Messages exceeding this
    /// length will be truncated. Default: 10,000 characters.
    /// </summary>
    public int MaxMessageLength { get; init; } = 10_000;

    /// <summary>
    /// Whether to log warnings when suspicious patterns (XSS, SQL injection,
    /// prompt injection) are detected in input. Default: true.
    /// </summary>
    public bool LogSuspiciousPatterns { get; init; } = true;

    /// <summary>
    /// Whether to strip all HTML tags from user input. Default: true.
    /// </summary>
    public bool StripHtmlTags { get; init; } = true;
}
