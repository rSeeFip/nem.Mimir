using FluentValidation;
using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Domain.Entities;
using nem.Mimir.Domain.ValueObjects;

namespace nem.Mimir.Application.Folders.Commands;

public sealed record RenameFolderCommand(Guid FolderId, string Name) : ICommand;

public sealed class RenameFolderCommandValidator : AbstractValidator<RenameFolderCommand>
{
    public RenameFolderCommandValidator()
    {
        RuleFor(x => x.FolderId)
            .NotEmpty().WithMessage("Folder ID is required.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Folder name is required.")
            .MaximumLength(200).WithMessage("Folder name must not exceed 200 characters.");
    }
}

internal sealed class RenameFolderCommandHandler
{
    private readonly IFolderRepository _folderRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;

    public RenameFolderCommandHandler(
        IFolderRepository folderRepository,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork)
    {
        _folderRepository = folderRepository;
        _currentUserService = currentUserService;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(RenameFolderCommand request, CancellationToken cancellationToken)
    {
        var folder = await _folderRepository.GetByIdAsync(FolderId.From(request.FolderId), cancellationToken)
            ?? throw new NotFoundException(nameof(Folder), request.FolderId);

        EnsureOwnership(folder, _currentUserService);

        folder.Rename(request.Name);

        await _folderRepository.UpdateAsync(folder, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static void EnsureOwnership(Folder folder, ICurrentUserService currentUserService)
    {
        var currentUserId = currentUserService.UserId
            ?? throw new ForbiddenAccessException("User is not authenticated.");

        if (folder.UserId != Guid.Parse(currentUserId))
            throw new ForbiddenAccessException();
    }
}
