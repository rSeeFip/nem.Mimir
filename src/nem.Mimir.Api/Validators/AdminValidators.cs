using FluentValidation;
using nem.Mimir.Domain.Enums;

namespace nem.Mimir.Api.Controllers;

/// <summary>
/// Validates <see cref="UpdateUserRoleRequest"/> ensuring the role is a valid <see cref="UserRole"/> value.
/// </summary>
public sealed class UpdateUserRoleRequestValidator : AbstractValidator<UpdateUserRoleRequest>
{
    private static readonly string[] ValidRoles = Enum.GetNames<UserRole>();

    public UpdateUserRoleRequestValidator()
    {
        RuleFor(x => x.Role)
            .NotEmpty().WithMessage("Role is required.")
            .Must(role => Enum.TryParse<UserRole>(role, ignoreCase: true, out _))
            .WithMessage($"Role must be one of: {string.Join(", ", ValidRoles)}.");
    }
}
