using FluentValidation;
using MediatR;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Mappings;
using nem.Mimir.Application.Common.Models;

namespace nem.Mimir.Application.SystemPrompts.Queries;

/// <summary>
/// Query to retrieve a paginated list of all system prompts.
/// </summary>
/// <param name="PageNumber">The page number to retrieve (1-based).</param>
/// <param name="PageSize">The number of system prompts per page.</param>
public sealed record ListSystemPromptsQuery(
    int PageNumber,
    int PageSize) : IQuery<PaginatedList<SystemPromptDto>>;

/// <summary>
/// Validates the <see cref="ListSystemPromptsQuery"/> ensuring pagination parameters are within acceptable ranges.
/// </summary>
public sealed class ListSystemPromptsQueryValidator : AbstractValidator<ListSystemPromptsQuery>
{
    public ListSystemPromptsQueryValidator()
    {
        RuleFor(x => x.PageNumber)
            .GreaterThanOrEqualTo(1).WithMessage("Page number must be at least 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 50).WithMessage("Page size must be between 1 and 50.");
    }
}

internal sealed class ListSystemPromptsQueryHandler : IRequestHandler<ListSystemPromptsQuery, PaginatedList<SystemPromptDto>>
{
    private readonly ISystemPromptRepository _repository;
    private readonly MimirMapper _mapper;

    public ListSystemPromptsQueryHandler(
        ISystemPromptRepository repository,
        MimirMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }

    public async Task<PaginatedList<SystemPromptDto>> Handle(ListSystemPromptsQuery request, CancellationToken cancellationToken)
    {
        var result = await _repository.GetAllAsync(request.PageNumber, request.PageSize, cancellationToken);

        var dtoItems = result.Items.Select(_mapper.MapToSystemPromptDto).ToList();

        return new PaginatedList<SystemPromptDto>(dtoItems, result.PageNumber, result.TotalPages, result.TotalCount);
    }
}
