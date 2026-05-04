using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using nem.Mimir.Application.Common.Sanitization;

namespace nem.Mimir.Telegram.Services;

internal sealed partial class TelegramSanitizationService : ISanitizationService
{
    private readonly SanitizationSettings _settings;
    private readonly ILogger<TelegramSanitizationService> _logger;

    public TelegramSanitizationService(
        IOptions<SanitizationSettings> settings,
        ILogger<TelegramSanitizationService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public string SanitizeUserInput(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        var sanitized = input.Trim();

        if (sanitized.Length > _settings.MaxMessageLength)
            sanitized = sanitized[.._settings.MaxMessageLength];

        if (_settings.StripHtmlTags)
            sanitized = HtmlTagRegex().Replace(sanitized, string.Empty);

        sanitized = ControlCharRegex().Replace(sanitized, string.Empty);

        if (_settings.LogSuspiciousPatterns && ContainsSuspiciousPatterns(input))
            _logger.LogWarning("Suspicious patterns detected in telegram input (length: {Length})", input.Length);

        return sanitized;
    }

    public string SanitizeLlmOutput(string output)
    {
        if (string.IsNullOrEmpty(output))
            return string.Empty;

        var sanitized = DangerousTagRegex().Replace(output, string.Empty);
        sanitized = EventHandlerRegex().Replace(sanitized, string.Empty);
        sanitized = PromptInjectionMarkerRegex().Replace(sanitized, string.Empty);
        sanitized = XssEntityRegex().Replace(sanitized, string.Empty);

        var maxOutputLength = _settings.MaxMessageLength * 4;
        if (sanitized.Length > maxOutputLength)
            sanitized = sanitized[..maxOutputLength];

        return sanitized;
    }

    public bool ContainsSuspiciousPatterns(string input)
    {
        if (string.IsNullOrEmpty(input))
            return false;

        return ScriptTagPattern().IsMatch(input)
            || SqlInjectionPattern().IsMatch(input)
            || PromptInjectionPattern().IsMatch(input);
    }

    [GeneratedRegex(@"<[^>]+>", RegexOptions.Compiled)]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex(@"[\x00-\x08\x0B\x0C\x0E-\x1F\x7F]", RegexOptions.Compiled)]
    private static partial Regex ControlCharRegex();

    [GeneratedRegex(@"<\s*/?\s*(script|iframe|embed|object|form)\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex DangerousTagRegex();

    [GeneratedRegex(@"\bon\w+\s*=\s*(?:""[^""]*""|'[^']*'|[^\s>]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex EventHandlerRegex();

    [GeneratedRegex(@"\[SYSTEM\]|\[INST\]|\[/INST\]|<<SYS>>|<</SYS>>|</s>|\[INST\s*\]", RegexOptions.Compiled)]
    private static partial Regex PromptInjectionMarkerRegex();

    [GeneratedRegex(@"&#x?[\da-fA-F]+;|&lt;\s*script", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex XssEntityRegex();

    [GeneratedRegex(@"<\s*script\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex ScriptTagPattern();

    [GeneratedRegex(
        @"\b(DROP\s+TABLE|UNION\s+SELECT|INSERT\s+INTO|DELETE\s+FROM|UPDATE\s+\w+\s+SET|;\s*DROP|'\s*OR\s+'1'\s*=\s*'1|--\s*$)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Multiline)]
    private static partial Regex SqlInjectionPattern();

    [GeneratedRegex(
        @"ignore\s+previous\s+instructions|ignore\s+all\s+instructions|system\s+prompt|you\s+are\s+now|act\s+as\s+if|forget\s+(everything|all|your|previous)|disregard\s+(all|previous|your)|override\s+instructions|\[SYSTEM\]|\[INST\]|<<SYS>>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex PromptInjectionPattern();
}
