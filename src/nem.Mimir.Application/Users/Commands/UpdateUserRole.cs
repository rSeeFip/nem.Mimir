using FluentValidation;
using MediatR;
using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Mappings;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Domain.Entities;
using nem.Mimir.Domain.Enums;

namespace nem.Mimir.Application.Users.Commands;

/// <summary>
/// Command to update a user's role. Requires administrator privileges.
/// </summary>
/// <param name="UserId">The unique identifier of the user whose role will be updated.</param>
/// <param name="NewRole">The new role to assign to the user.</param>
public sealed record UpdateUserRoleCommand(
    Guid UserId,
    UserRole NewRole) : ICommand<UserDto>;

/// <summary>
/// Validates the <see cref="UpdateUserRoleCommand"/> ensuring user ID is provided and role is valid.
/// </summary>
public sealed class UpdateUserRoleCommandValidator : AbstractValidator<UpdateUserRoleCommand>
{
    public UpdateUserRoleCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID is required.");

        RuleFor(x => x.NewRole)
            .IsInEnum().WithMessage("A valid role is required.");
    }
}

internal sealed class UpdateUserRoleCommandHandler : IRequestHandler<UpdateUserRoleCommand, UserDto>
{
    private readonly IUserRepository _repository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly MimirMapper _mapper;

    public UpdateUserRoleCommandHandler(
        IUserRepository repository,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork,
        MimirMapper mapper)
    {
        _repository = repository;
        _currentUserService = currentUserService;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<UserDto> Handle(UpdateUserRoleCommand request, CancellationToken cancellationToken)
    {
        EnsureAdminRole();

        var user = await _repository.GetByIdAsync(request.UserId, cancellationToken)
            ?? throw new NotFoundException(nameof(User), request.UserId);

        user.ChangeRole(request.NewRole);

        await _repository.UpdateAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.MapToUserDto(user);
    }

    private void EnsureAdminRole()
    {
        if (!_currentUserService.IsAuthenticated)
        {
            throw new ForbiddenAccessException("User is not authenticated.");
        }

        if (!_currentUserService.Roles.Contains("Admin"))
        {
            throw new ForbiddenAccessException("Only administrators can perform this action.");
        }
    }
}
