using FluentValidation;
using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Mappings;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Domain.Entities;

namespace nem.Mimir.Application.Prompts.Queries;

public sealed record GetPromptByCommandQuery(string Command) : IQuery<PromptTemplateDto>;

public sealed class GetPromptByCommandQueryValidator : AbstractValidator<GetPromptByCommandQuery>
{
    public GetPromptByCommandQueryValidator()
    {
        RuleFor(x => x.Command)
            .NotEmpty().WithMessage("Prompt command is required.")
            .Matches("^/[a-z0-9-]+$").WithMessage("Prompt command must match '/command-name'.");
    }
}

internal sealed class GetPromptByCommandQueryHandler
{
    private readonly IPromptTemplateRepository _repository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly MimirMapper _mapper;

    public GetPromptByCommandQueryHandler(
        IPromptTemplateRepository repository,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork,
        MimirMapper mapper)
    {
        _repository = repository;
        _currentUserService = currentUserService;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<PromptTemplateDto> Handle(GetPromptByCommandQuery request, CancellationToken cancellationToken)
    {
        var userId = ResolveCurrentUserId(_currentUserService);

        var promptTemplate = await _repository
            .GetByCommandForUserOrSharedAsync(request.Command, userId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(PromptTemplate), request.Command);

        promptTemplate.IncrementUsage();

        await _repository.UpdateAsync(promptTemplate, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return _mapper.MapToPromptTemplateDto(promptTemplate);
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
