using AutoMapper;
using FluentValidation;
using MediatR;
using Mimir.Application.Common.Interfaces;
using Mimir.Application.Common.Models;

namespace Mimir.Application.SystemPrompts.Queries;

public sealed record ListSystemPromptsQuery(
    int PageNumber,
    int PageSize) : IQuery<PaginatedList<SystemPromptDto>>;

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
    private readonly IMapper _mapper;

    public ListSystemPromptsQueryHandler(
        ISystemPromptRepository repository,
        IMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }

    public async Task<PaginatedList<SystemPromptDto>> Handle(ListSystemPromptsQuery request, CancellationToken cancellationToken)
    {
        var result = await _repository.GetAllAsync(request.PageNumber, request.PageSize, cancellationToken);

        var dtoItems = _mapper.Map<IReadOnlyCollection<SystemPromptDto>>(result.Items);

        return new PaginatedList<SystemPromptDto>(dtoItems, result.PageNumber, result.TotalPages, result.TotalCount);
    }
}
