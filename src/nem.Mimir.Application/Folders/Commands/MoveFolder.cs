using FluentValidation;
using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Domain.Entities;
using nem.Mimir.Domain.ValueObjects;

namespace nem.Mimir.Application.Folders.Commands;

public sealed record MoveFolderCommand(Guid FolderId, Guid? ParentId) : ICommand;

public sealed class MoveFolderCommandValidator : AbstractValidator<MoveFolderCommand>
{
    public MoveFolderCommandValidator()
    {
        RuleFor(x => x.FolderId)
            .NotEmpty().WithMessage("Folder ID is required.");

        RuleFor(x => x)
            .Must(x => !x.ParentId.HasValue || x.ParentId.Value != x.FolderId)
            .WithMessage("Folder cannot be moved into itself.");
    }
}

internal sealed class MoveFolderCommandHandler
{
    private readonly IFolderRepository _folderRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;

    public MoveFolderCommandHandler(
        IFolderRepository folderRepository,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork)
    {
        _folderRepository = folderRepository;
        _currentUserService = currentUserService;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(MoveFolderCommand request, CancellationToken cancellationToken)
    {
        var folder = await _folderRepository.GetByIdAsync(FolderId.From(request.FolderId), cancellationToken)
            ?? throw new NotFoundException(nameof(Folder), request.FolderId);

        EnsureOwnership(folder, _currentUserService);

        FolderId? parentId = null;
        if (request.ParentId.HasValue)
        {
            var parentTypedId = FolderId.From(request.ParentId.Value);
            var parent = await _folderRepository.GetByIdAsync(parentTypedId, cancellationToken)
                ?? throw new NotFoundException(nameof(Folder), request.ParentId.Value);

            EnsureOwnership(parent, _currentUserService);
            parentId = parentTypedId;
        }

        folder.SetParent(parentId);

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
