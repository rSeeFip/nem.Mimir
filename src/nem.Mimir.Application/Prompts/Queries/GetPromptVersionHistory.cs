using FluentValidation;
using nem.Contracts.Identity;
using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Models;

namespace nem.Mimir.Application.Prompts.Queries;

public sealed record GetPromptVersionHistoryQuery(PromptTemplateId Id) : IQuery<IReadOnlyList<PromptTemplateVersionDto>>;

public sealed class GetPromptVersionHistoryQueryValidator : AbstractValidator<GetPromptVersionHistoryQuery>
{
    public GetPromptVersionHistoryQueryValidator()
    {
        RuleFor(x => x.Id)
            .NotEqual(PromptTemplateId.Empty).WithMessage("Prompt template ID is required.");
    }
}

internal sealed class GetPromptVersionHistoryQueryHandler
{
    private readonly IPromptTemplateRepository _repository;
    private readonly ICurrentUserService _currentUserService;

    public GetPromptVersionHistoryQueryHandler(
        IPromptTemplateRepository repository,
        ICurrentUserService currentUserService)
    {
        _repository = repository;
        _currentUserService = currentUserService;
    }

    public async Task<IReadOnlyList<PromptTemplateVersionDto>> Handle(GetPromptVersionHistoryQuery request, CancellationToken cancellationToken)
    {
        var userId = ResolveCurrentUserId(_currentUserService);

        var promptTemplate = await _repository
            .GetByIdAsync(request.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("PromptTemplate", request.Id);

        if (promptTemplate.UserId != userId && !promptTemplate.IsShared)
        {
            throw new NotFoundException("PromptTemplate", request.Id);
        }

        return promptTemplate.VersionHistory
            .OrderByDescending(v => v.Timestamp)
            .Select(v => new PromptTemplateVersionDto(v.Content, v.Timestamp))
            .ToList();
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
