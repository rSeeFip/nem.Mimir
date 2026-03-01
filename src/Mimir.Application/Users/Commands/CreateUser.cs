using FluentValidation;
using MediatR;
using Mimir.Application.Common.Interfaces;
using Mimir.Application.Common.Mappings;
using Mimir.Application.Common.Models;
using Mimir.Domain.Entities;
using Mimir.Domain.Enums;

namespace Mimir.Application.Users.Commands;

/// <summary>
/// Command to create a new user account.
/// </summary>
/// <param name="Email">The email address for the new user.</param>
/// <param name="DisplayName">The display name for the new user.</param>
/// <param name="Role">The role to assign to the new user (defaults to User).</param>
public sealed record CreateUserCommand(
    string Email,
    string DisplayName,
    UserRole Role = UserRole.User) : ICommand<UserDto>;

/// <summary>
/// Validates the <see cref="CreateUserCommand"/> ensuring email and display name are valid.
/// </summary>
public sealed class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("A valid email address is required.");

        RuleFor(x => x.DisplayName)
            .NotEmpty().WithMessage("Display name is required.")
            .MaximumLength(100).WithMessage("Display name must not exceed 100 characters.");
    }
}

internal sealed class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, UserDto>
{
    private readonly IUserRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly MimirMapper _mapper;

    public CreateUserCommandHandler(
        IUserRepository repository,
        IUnitOfWork unitOfWork,
        MimirMapper mapper)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<UserDto> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        var user = User.Create(request.DisplayName, request.Email, request.Role);

        await _repository.CreateAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.MapToUserDto(user);
    }
}
