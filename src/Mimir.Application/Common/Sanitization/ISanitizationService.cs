namespace Mimir.Application.Common.Sanitization;

/// <summary>
/// Service for sanitizing user input and LLM output to prevent XSS,
/// prompt injection markers, and enforce message length limits.
/// </summary>
public interface ISanitizationService
{
    /// <summary>
    /// Sanitizes user input before persistence: trims whitespace, enforces max length,
    /// strips HTML tags, and removes control characters.
    /// </summary>
    /// <param name="input">The raw user input.</param>
    /// <returns>The sanitized input string.</returns>
    string SanitizeUserInput(string input);

    /// <summary>
    /// Sanitizes LLM output before sending to the client: strips XSS vectors,
    /// prompt injection markers, and enforces max output length while preserving
    /// markdown-friendly HTML tags.
    /// </summary>
    /// <param name="output">The raw LLM output.</param>
    /// <returns>The sanitized output string.</returns>
    string SanitizeLlmOutput(string output);

    /// <summary>
    /// Checks whether the input contains suspicious patterns such as script tags,
    /// SQL injection keywords, or prompt injection attempts. Used for logging only,
    /// not for blocking.
    /// </summary>
    /// <param name="input">The input to check.</param>
    /// <returns>True if suspicious patterns are detected; otherwise false.</returns>
    bool ContainsSuspiciousPatterns(string input);
}
