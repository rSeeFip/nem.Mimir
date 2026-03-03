using FluentValidation;
using Mimir.Api.Validators;

namespace Mimir.Api.Controllers;

/// <summary>
/// Validates <see cref="CreateSystemPromptRequest"/> ensuring name and template are non-empty
/// and within length limits.
/// </summary>
public sealed class CreateSystemPromptRequestValidator : AbstractValidator<CreateSystemPromptRequest>
{
    public CreateSystemPromptRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(200).WithMessage("Name must not exceed 200 characters.");

        RuleFor(x => x.Template)
            .NotEmpty().WithMessage("Template is required.")
            .MaximumLength(100_000).WithMessage("Template must not exceed 100,000 characters.")
            .NoPromptInjection();

        RuleFor(x => x.Description)
            .MaximumLength(1_000).WithMessage("Description must not exceed 1,000 characters.")
            .When(x => x.Description is not null);
    }
}

/// <summary>
/// Validates <see cref="UpdateSystemPromptRequest"/> ensuring name and template are non-empty
/// and within length limits.
/// </summary>
public sealed class UpdateSystemPromptRequestValidator : AbstractValidator<UpdateSystemPromptRequest>
{
    public UpdateSystemPromptRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(200).WithMessage("Name must not exceed 200 characters.");

        RuleFor(x => x.Template)
            .NotEmpty().WithMessage("Template is required.")
            .MaximumLength(100_000).WithMessage("Template must not exceed 100,000 characters.")
            .NoPromptInjection();

        RuleFor(x => x.Description)
            .MaximumLength(1_000).WithMessage("Description must not exceed 1,000 characters.")
            .When(x => x.Description is not null);
    }
}

/// <summary>
/// Validates <see cref="RenderSystemPromptRequest"/>. Variables are optional; when provided,
/// each key and value must be within reasonable length limits.
/// </summary>
public sealed class RenderSystemPromptRequestValidator : AbstractValidator<RenderSystemPromptRequest>
{
    private const int MaxVariableKeyLength = 100;
    private const int MaxVariableValueLength = 10_000;

    public RenderSystemPromptRequestValidator()
    {
        RuleFor(x => x.Variables)
            .Must(vars =>
                vars is null ||
                vars.Keys.All(k => !string.IsNullOrWhiteSpace(k) && k.Length <= MaxVariableKeyLength))
            .WithMessage($"Each variable key must be non-empty and not exceed {MaxVariableKeyLength} characters.")
            .Must(vars =>
                vars is null ||
                vars.Values.All(v => v is not null && v.Length <= MaxVariableValueLength))
            .WithMessage($"Each variable value must not exceed {MaxVariableValueLength:N0} characters.")
            .When(x => x.Variables is not null);
    }
}
