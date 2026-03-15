using FluentValidation;
using nem.Mimir.Api.Validators;

namespace nem.Mimir.Api.Controllers;

/// <summary>
/// Validates <see cref="CreateConversationRequest"/> ensuring the title is non-empty and within
/// length limits. Optional fields (SystemPrompt, Model) are validated when present.
/// </summary>
public sealed class CreateConversationRequestValidator : AbstractValidator<CreateConversationRequest>
{
    public CreateConversationRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required.")
            .MaximumLength(200).WithMessage("Title must not exceed 200 characters.");

        // System prompt is optional but if provided must not exceed prompt length limit
        RuleFor(x => x.SystemPrompt)
            .MaximumLength(100_000).WithMessage("System prompt must not exceed 100,000 characters.")
            .NoPromptInjection()
            .When(x => x.SystemPrompt is not null);

        // Model name is optional but if provided must be a valid identifier
        RuleFor(x => x.Model)
            .ValidModelName()
            .MaximumLength(200).WithMessage("Model identifier must not exceed 200 characters.")
            .When(x => x.Model is not null);
    }
}

/// <summary>
/// Validates <see cref="UpdateConversationTitleRequest"/> ensuring the title is non-empty.
/// </summary>
public sealed class UpdateConversationTitleRequestValidator : AbstractValidator<UpdateConversationTitleRequest>
{
    public UpdateConversationTitleRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required.")
            .MaximumLength(200).WithMessage("Title must not exceed 200 characters.");
    }
}
