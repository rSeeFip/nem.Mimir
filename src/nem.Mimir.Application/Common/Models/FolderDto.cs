using FluentValidation;

namespace nem.Mimir.Application.Common.Models;

public sealed record FolderDto(
    Guid Id,
    Guid UserId,
    string Name,
    Guid? ParentId,
    bool IsExpanded,
    int ItemCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed class FolderDtoValidator : AbstractValidator<FolderDto>
{
    public FolderDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Folder name is required.")
            .MaximumLength(200).WithMessage("Folder name must not exceed 200 characters.");
    }
}
