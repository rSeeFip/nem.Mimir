using FluentValidation;
using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Mappings;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Domain.Entities;

namespace nem.Mimir.Application.Memories.Commands;

public sealed record CreateUserMemoryCommand(
    string Content,
    string? Context) : ICommand<UserMemoryDto>;

public sealed class CreateUserMemoryCommandValidator : AbstractValidator<CreateUserMemoryCommand>
{
    public CreateUserMemoryCommandValidator()
    {
        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("Content is required.")
            .MaximumLength(4000).WithMessage("Content must not exceed 4000 characters.");

        RuleFor(x => x.Context)
            .MaximumLength(1000).WithMessage("Context must not exceed 1000 characters.")
            .When(x => x.Context is not null);
    }
}

internal sealed class CreateUserMemoryCommandHandler
{
    private readonly IUserMemoryRepository _repository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly MimirMapper _mapper;

    public CreateUserMemoryCommandHandler(
        IUserMemoryRepository repository,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork,
        MimirMapper mapper)
    {
        _repository = repository;
        _currentUserService = currentUserService;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<UserMemoryDto> Handle(CreateUserMemoryCommand request, CancellationToken cancellationToken)
    {
        var userId = ResolveCurrentUserId(_currentUserService);
        var memory = UserMemory.Create(userId, request.Content, request.Context);

        await _repository.CreateAsync(memory, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return _mapper.MapToUserMemoryDto(memory);
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
