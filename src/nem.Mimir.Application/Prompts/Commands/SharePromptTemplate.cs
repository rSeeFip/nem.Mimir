using FluentValidation;
using nem.Contracts.Identity;
using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Domain.Entities;

namespace nem.Mimir.Application.Prompts.Commands;

public sealed record SharePromptTemplateCommand(PromptTemplateId Id, bool IsShared) : ICommand;

public sealed class SharePromptTemplateCommandValidator : AbstractValidator<SharePromptTemplateCommand>
{
    public SharePromptTemplateCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEqual(PromptTemplateId.Empty).WithMessage("Prompt template ID is required.");
    }
}

internal sealed class SharePromptTemplateCommandHandler
{
    private readonly IPromptTemplateRepository _repository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;

    public SharePromptTemplateCommandHandler(
        IPromptTemplateRepository repository,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _currentUserService = currentUserService;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(SharePromptTemplateCommand request, CancellationToken cancellationToken)
    {
        var userId = ResolveCurrentUserId(_currentUserService);

        var promptTemplate = await _repository
            .GetByIdForUserAsync(request.Id, userId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(PromptTemplate), request.Id);

        promptTemplate.SetShared(request.IsShared);

        await _repository.UpdateAsync(promptTemplate, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
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
