using System.Text.RegularExpressions;
using FluentValidation;

namespace Mimir.Api.Validators;

/// <summary>
/// Custom FluentValidation extensions for common API-level validation rules.
/// </summary>
public static partial class ValidatorExtensions
{
    /// <summary>
    /// Valid model name pattern: alphanumeric characters, hyphens, dots, underscores, colons, and forward-slashes.
    /// Covers common LLM model IDs such as "gpt-4o", "qwen-2.5-72b", "meta-llama/Meta-Llama-3.1-70B", etc.
    /// </summary>
    private static readonly Regex ModelNameRegex = GenerateModelNameRegex();

    /// <summary>
    /// Patterns that indicate attempts to hijack the system prompt or manipulate the LLM context.
    /// Kept minimal — only the most obvious injection attack vectors are blocked.
    /// </summary>
    private static readonly Regex PromptInjectionRegex = GeneratePromptInjectionRegex();

    /// <summary>
    /// Validates that the string is a valid LLM model identifier.
    /// Allows alphanumeric characters, hyphens, dots, underscores, colons, and forward-slashes.
    /// </summary>
    public static IRuleBuilderOptions<T, string?> ValidModelName<T>(this IRuleBuilder<T, string?> ruleBuilder)
    {
        return ruleBuilder
            .Must(value => value is null || ModelNameRegex.IsMatch(value))
            .WithMessage("'{PropertyName}' must be a valid model identifier (alphanumeric, hyphens, dots, underscores, colons, or forward-slashes only).");
    }

    /// <summary>
    /// Validates that the string does not contain prompt injection patterns.
    /// Blocks obvious attempts to override or escape the system prompt context.
    /// </summary>
    public static IRuleBuilderOptions<T, string?> NoPromptInjection<T>(this IRuleBuilder<T, string?> ruleBuilder)
    {
        return ruleBuilder
            .Must(value => value is null || !PromptInjectionRegex.IsMatch(value))
            .WithMessage("'{PropertyName}' contains a disallowed pattern.");
    }

    [GeneratedRegex(@"^[a-zA-Z0-9\-._:/@]+$", RegexOptions.Compiled)]
    private static partial Regex GenerateModelNameRegex();

    // Minimal prompt injection guard — targets the most common manipulation vectors:
    //   1. "<|im_start|>system" / "<|im_end|>" — ChatML control tokens
    //   2. "[INST]" / "<<SYS>>" — Llama/Mistral system delimiters
    //   3. "ignore (all )?(previous|above|prior) instructions" — classic direct override
    //   4. "disregard (all )?(previous|above|prior) instructions" — variant
    //   5. "you are now" — common persona-switch opener
    //   6. "act as (a|an)" — persona-switch opener
    //   7. Bare "<system>" / "</system>" XML tags — XML-style injection
    [GeneratedRegex(
        @"<\|im_start\|>system|<\|im_end\|>|\[INST\]|<<SYS>>|ignore\s+(all\s+)?(previous|above|prior)\s+instructions|disregard\s+(all\s+)?(previous|above|prior)\s+instructions|you\s+are\s+now\s|act\s+as\s+an?\s|<system>|</system>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex GeneratePromptInjectionRegex();
}
