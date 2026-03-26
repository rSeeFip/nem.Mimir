using FluentValidation;
using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Domain.Entities;
using nem.Mimir.Domain.ValueObjects;

namespace nem.Mimir.Application.Folders.Commands;

public sealed record DeleteFolderCommand(Guid FolderId) : ICommand;

public sealed class DeleteFolderCommandValidator : AbstractValidator<DeleteFolderCommand>
{
    public DeleteFolderCommandValidator()
    {
        RuleFor(x => x.FolderId)
            .NotEmpty().WithMessage("Folder ID is required.");
    }
}

internal sealed class DeleteFolderCommandHandler
{
    private readonly IFolderRepository _folderRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteFolderCommandHandler(
        IFolderRepository folderRepository,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork)
    {
        _folderRepository = folderRepository;
        _currentUserService = currentUserService;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(DeleteFolderCommand request, CancellationToken cancellationToken)
    {
        var folder = await _folderRepository.GetByIdAsync(FolderId.From(request.FolderId), cancellationToken)
            ?? throw new NotFoundException(nameof(Folder), request.FolderId);

        EnsureOwnership(folder, _currentUserService);

        await _folderRepository.DeleteAsync(FolderId.From(request.FolderId), cancellationToken);
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
