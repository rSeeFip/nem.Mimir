using AutoMapper;
using FluentValidation;
using MediatR;
using Mimir.Application.Common.Exceptions;
using Mimir.Application.Common.Interfaces;
using Mimir.Application.Common.Models;
using Mimir.Domain.Entities;
using Mimir.Domain.Enums;

namespace Mimir.Application.Users.Commands;

public sealed record UpdateUserRoleCommand(
    Guid UserId,
    UserRole NewRole) : ICommand<UserDto>;

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
    private readonly IMapper _mapper;

    public UpdateUserRoleCommandHandler(
        IUserRepository repository,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork,
        IMapper mapper)
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

        return _mapper.Map<UserDto>(user);
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
