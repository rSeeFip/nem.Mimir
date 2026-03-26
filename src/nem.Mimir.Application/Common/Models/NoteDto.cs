using FluentValidation;

namespace nem.Mimir.Application.Common.Models;

public sealed record NoteDto(
    Guid Id,
    Guid UserId,
    string Title,
    string Content,
    IReadOnlyList<string> Tags,
    string AccessLevel,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record NoteListDto(
    Guid Id,
    string Title,
    IReadOnlyList<string> Tags,
    string AccessLevel,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record NoteVersionDto(
    Guid Id,
    Guid NoteId,
    string Content,
    string? ChangeDescription,
    Guid CreatedByUserId,
    DateTimeOffset CreatedAt);

public sealed record NoteCollaboratorDto(
    Guid UserId,
    string Permission,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed class NoteDtoValidator : AbstractValidator<NoteDto>
{
    public NoteDtoValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Note title is required.")
            .MaximumLength(200).WithMessage("Note title must not exceed 200 characters.");

        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("Note content is required.");

        RuleFor(x => x.AccessLevel)
            .NotEmpty().WithMessage("Access level is required.");
    }
}

public sealed class NoteVersionDtoValidator : AbstractValidator<NoteVersionDto>
{
    public NoteVersionDtoValidator()
    {
        RuleFor(x => x.NoteId)
            .NotEmpty().WithMessage("Note ID is required.");

        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("Version content is required.");
    }
}

public sealed class NoteCollaboratorDtoValidator : AbstractValidator<NoteCollaboratorDto>
{
    public NoteCollaboratorDtoValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("Collaborator user ID is required.");

        RuleFor(x => x.Permission)
            .NotEmpty().WithMessage("Collaborator permission is required.");
    }
}
