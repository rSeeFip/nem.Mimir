using FluentValidation;
using Mimir.Api.Validators;

namespace Mimir.Api.Controllers;

/// <summary>
/// Validates <see cref="SendMessageRequest"/> ensuring content is non-empty and within length
/// limits. Includes chat-specific validation: prompt injection guard and model name check.
/// </summary>
public sealed class SendMessageRequestValidator : AbstractValidator<SendMessageRequest>
{
    /// <summary>
    /// Maximum allowed content length for a single message (100,000 characters).
    /// This is an API-layer guard; the application layer enforces its own 32,000-char limit.
    /// </summary>
    private const int MaxContentLength = 100_000;

    public SendMessageRequestValidator()
    {
        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("Message content is required.")
            .MaximumLength(MaxContentLength)
                .WithMessage($"Message content must not exceed {MaxContentLength:N0} characters.")
            .NoPromptInjection();

        // Model name is optional but if provided must be a valid identifier
        RuleFor(x => x.Model)
            .ValidModelName()
            .MaximumLength(200).WithMessage("Model identifier must not exceed 200 characters.")
            .When(x => x.Model is not null);
    }
}
