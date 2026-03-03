using FluentValidation;
using Mimir.Api.Validators;

namespace Mimir.Api.Controllers;

/// <summary>
/// Validates <see cref="LoadPluginRequest"/> ensuring the assembly path is non-empty and within
/// a reasonable length limit. Only absolute paths pointing to .dll files are accepted.
/// </summary>
public sealed class LoadPluginRequestValidator : AbstractValidator<LoadPluginRequest>
{
    public LoadPluginRequestValidator()
    {
        RuleFor(x => x.AssemblyPath)
            .NotEmpty().WithMessage("Assembly path is required.")
            .MaximumLength(500).WithMessage("Assembly path must not exceed 500 characters.")
            .Must(path => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Assembly path must point to a .dll file.");
    }
}

/// <summary>
/// Validates <see cref="ExecutePluginRequest"/> ensuring the user ID is a valid GUID and that
/// parameter keys are within reasonable bounds.
/// </summary>
public sealed class ExecutePluginRequestValidator : AbstractValidator<ExecutePluginRequest>
{
    private const int MaxParameterKeyLength = 100;

    public ExecutePluginRequestValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("UserId is required.")
            .Must(id => Guid.TryParse(id, out _))
            .WithMessage("UserId must be a valid GUID.");

        RuleFor(x => x.Parameters)
            .NotNull().WithMessage("Parameters must not be null.")
            .Must(parameters =>
                parameters is null ||
                parameters.Keys.All(k => !string.IsNullOrWhiteSpace(k) && k.Length <= MaxParameterKeyLength))
            .WithMessage($"Each parameter key must be non-empty and not exceed {MaxParameterKeyLength} characters.");
    }
}
