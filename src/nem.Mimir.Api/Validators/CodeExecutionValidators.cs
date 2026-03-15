using FluentValidation;

namespace nem.Mimir.Api.Controllers;

/// <summary>
/// Validates <see cref="ExecuteCodeRequest"/> ensuring language and code are non-empty and within
/// the accepted constraints. Mirrors limits enforced by the application-layer
/// <c>ExecuteCodeCommandValidator</c> to provide early rejection at the API boundary.
/// </summary>
public sealed class ExecuteCodeRequestValidator : AbstractValidator<ExecuteCodeRequest>
{
    private static readonly string[] AllowedLanguages = ["python", "javascript"];
    private const int MaxCodeLength = 50_000; // ~50KB character limit (chars, not bytes)

    public ExecuteCodeRequestValidator()
    {
        RuleFor(x => x.Language)
            .NotEmpty().WithMessage("Language is required.")
            .Must(lang => AllowedLanguages.Contains(lang.ToLowerInvariant()))
            .WithMessage("Language must be 'python' or 'javascript'.");

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Code is required.")
            .MaximumLength(MaxCodeLength)
                .WithMessage($"Code must not exceed {MaxCodeLength:N0} characters.");
    }
}
