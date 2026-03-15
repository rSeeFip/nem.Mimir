using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using nem.Mimir.Application.Common.Sanitization;

namespace nem.Mimir.Infrastructure.Services;

/// <summary>
/// Sanitizes user input and LLM output to prevent XSS, strip prompt injection
/// markers, and enforce message length limits.
/// </summary>
internal sealed partial class SanitizationService : ISanitizationService
{
    private readonly SanitizationSettings _settings;
    private readonly ILogger<SanitizationService> _logger;

    public SanitizationService(
        IOptions<SanitizationSettings> settings,
        ILogger<SanitizationService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public string SanitizeUserInput(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        var sanitized = input.Trim();

        // Enforce max length
        if (sanitized.Length > _settings.MaxMessageLength)
        {
            sanitized = sanitized[.._settings.MaxMessageLength];
        }

        // Strip HTML tags if enabled
        if (_settings.StripHtmlTags)
        {
            sanitized = HtmlTagRegex().Replace(sanitized, string.Empty);
        }

        // Remove null bytes and control characters (preserve newline, tab, carriage return)
        sanitized = ControlCharRegex().Replace(sanitized, string.Empty);

        // Log suspicious patterns if enabled
        if (_settings.LogSuspiciousPatterns && ContainsSuspiciousPatterns(input))
        {
            _logger.LogWarning(
                "Suspicious patterns detected in user input (length: {Length})",
                input.Length);
        }

        return sanitized;
    }

    /// <inheritdoc />
    public string SanitizeLlmOutput(string output)
    {
        if (string.IsNullOrEmpty(output))
            return string.Empty;

        var sanitized = output;

        // Strip dangerous HTML tags: script, iframe, embed, object, form
        sanitized = DangerousTagRegex().Replace(sanitized, string.Empty);

        // Strip event handlers (onclick, onerror, onload, etc.)
        sanitized = EventHandlerRegex().Replace(sanitized, string.Empty);

        // Strip prompt injection markers
        sanitized = PromptInjectionMarkerRegex().Replace(sanitized, string.Empty);

        // Remove HTML entities that could be used for XSS (&#xx; patterns and &lt;script)
        sanitized = XssEntityRegex().Replace(sanitized, string.Empty);

        // Enforce max output length (LLM responses can be longer: MaxMessageLength * 4)
        var maxOutputLength = _settings.MaxMessageLength * 4;
        if (sanitized.Length > maxOutputLength)
        {
            sanitized = sanitized[..maxOutputLength];
        }

        return sanitized;
    }

    /// <inheritdoc />
    public bool ContainsSuspiciousPatterns(string input)
    {
        if (string.IsNullOrEmpty(input))
            return false;

        // Check for script tags
        if (ScriptTagPattern().IsMatch(input))
            return true;

        // Check for SQL injection keywords
        if (SqlInjectionPattern().IsMatch(input))
            return true;

        // Check for prompt injection attempts
        if (PromptInjectionPattern().IsMatch(input))
            return true;

        return false;
    }

    // ── Source-generated Regex patterns ──────────────────────────────────────

    /// <summary>Matches all HTML tags.</summary>
    [GeneratedRegex(@"<[^>]+>", RegexOptions.Compiled)]
    private static partial Regex HtmlTagRegex();

    /// <summary>Matches null bytes and control characters except \n, \r, \t.</summary>
    [GeneratedRegex(@"[\x00-\x08\x0B\x0C\x0E-\x1F\x7F]", RegexOptions.Compiled)]
    private static partial Regex ControlCharRegex();

    /// <summary>Matches dangerous HTML tags (script, iframe, embed, object, form) and their content.</summary>
    [GeneratedRegex(
        @"<\s*/?\s*(script|iframe|embed|object|form)\b[^>]*>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex DangerousTagRegex();

    /// <summary>Matches HTML event handler attributes (onclick, onerror, onload, etc.).</summary>
    [GeneratedRegex(
        @"\bon\w+\s*=\s*(?:""[^""]*""|'[^']*'|[^\s>]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex EventHandlerRegex();

    /// <summary>Matches common prompt injection markers.</summary>
    [GeneratedRegex(
        @"\[SYSTEM\]|\[INST\]|\[/INST\]|<<SYS>>|<</SYS>>|</s>|\[INST\s*\]",
        RegexOptions.Compiled)]
    private static partial Regex PromptInjectionMarkerRegex();

    /// <summary>Matches HTML entities used for XSS (&#nnn; patterns and &lt;script).</summary>
    [GeneratedRegex(
        @"&#x?[\da-fA-F]+;|&lt;\s*script",
        RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex XssEntityRegex();

    // ── Suspicious pattern detection regexes ────────────────────────────────

    /// <summary>Detects script tags in any form.</summary>
    [GeneratedRegex(
        @"<\s*script\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex ScriptTagPattern();

    /// <summary>Detects common SQL injection patterns.</summary>
    [GeneratedRegex(
        @"\b(DROP\s+TABLE|UNION\s+SELECT|INSERT\s+INTO|DELETE\s+FROM|UPDATE\s+\w+\s+SET|;\s*DROP|'\s*OR\s+'1'\s*=\s*'1|--\s*$)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Multiline)]
    private static partial Regex SqlInjectionPattern();

    /// <summary>Detects prompt injection attempts.</summary>
    [GeneratedRegex(
        @"ignore\s+previous\s+instructions|ignore\s+all\s+instructions|system\s+prompt|you\s+are\s+now|act\s+as\s+if|forget\s+(everything|all|your|previous)|disregard\s+(all|previous|your)|override\s+instructions|\[SYSTEM\]|\[INST\]|<<SYS>>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex PromptInjectionPattern();
}
