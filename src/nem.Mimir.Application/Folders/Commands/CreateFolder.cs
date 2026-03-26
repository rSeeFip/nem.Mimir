using FluentValidation;
using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Mappings;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Domain.Entities;
using nem.Mimir.Domain.ValueObjects;

namespace nem.Mimir.Application.Folders.Commands;

public sealed record CreateFolderCommand(
    string Name,
    Guid? ParentId) : ICommand<FolderDto>;

public sealed class CreateFolderCommandValidator : AbstractValidator<CreateFolderCommand>
{
    public CreateFolderCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Folder name is required.")
            .MaximumLength(200).WithMessage("Folder name must not exceed 200 characters.");
    }
}

internal sealed class CreateFolderCommandHandler
{
    private readonly IFolderRepository _folderRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly MimirMapper _mapper;

    public CreateFolderCommandHandler(
        IFolderRepository folderRepository,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork,
        MimirMapper mapper)
    {
        _folderRepository = folderRepository;
        _currentUserService = currentUserService;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<FolderDto> Handle(CreateFolderCommand request, CancellationToken cancellationToken)
    {
        var userId = ResolveCurrentUserId(_currentUserService);

        FolderId? parentId = null;
        if (request.ParentId.HasValue)
        {
            parentId = FolderId.From(request.ParentId.Value);
            var parentFolder = await _folderRepository.GetByIdAsync(parentId.Value, cancellationToken)
                ?? throw new NotFoundException(nameof(Folder), request.ParentId.Value);

            if (parentFolder.UserId != userId)
                throw new ForbiddenAccessException();
        }

        var folder = Folder.Create(userId, request.Name, parentId);

        await _folderRepository.CreateAsync(folder, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.MapToFolderDto(folder, 0);
    }

    private static Guid ResolveCurrentUserId(ICurrentUserService currentUserService)
    {
        var userId = currentUserService.UserId
            ?? throw new ForbiddenAccessException("User is not authenticated.");

        if (!Guid.TryParse(userId, out var parsedUserId))
            throw new ForbiddenAccessException("Current user identifier is invalid.");

        return parsedUserId;
    }
}
