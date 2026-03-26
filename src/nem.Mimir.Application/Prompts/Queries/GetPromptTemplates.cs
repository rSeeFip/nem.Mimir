using FluentValidation;
using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Mappings;
using nem.Mimir.Application.Common.Models;

namespace nem.Mimir.Application.Prompts.Queries;

public sealed record GetPromptTemplatesQuery(
    int PageNumber,
    int PageSize,
    string? Search,
    bool? IsShared,
    IReadOnlyList<string>? Tags) : IQuery<PaginatedList<PromptTemplateListDto>>;

public sealed class GetPromptTemplatesQueryValidator : AbstractValidator<GetPromptTemplatesQuery>
{
    public GetPromptTemplatesQueryValidator()
    {
        RuleFor(x => x.PageNumber)
            .GreaterThanOrEqualTo(1).WithMessage("Page number must be at least 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("Page size must be between 1 and 100.");

        RuleForEach(x => x.Tags)
            .MaximumLength(50).WithMessage("Tag cannot exceed 50 characters.");
    }
}

internal sealed class GetPromptTemplatesQueryHandler
{
    private readonly IPromptTemplateRepository _repository;
    private readonly ICurrentUserService _currentUserService;
    private readonly MimirMapper _mapper;

    public GetPromptTemplatesQueryHandler(
        IPromptTemplateRepository repository,
        ICurrentUserService currentUserService,
        MimirMapper mapper)
    {
        _repository = repository;
        _currentUserService = currentUserService;
        _mapper = mapper;
    }

    public async Task<PaginatedList<PromptTemplateListDto>> Handle(GetPromptTemplatesQuery request, CancellationToken cancellationToken)
    {
        var userId = ResolveCurrentUserId(_currentUserService);

        var result = await _repository
            .GetAccessibleAsync(userId, request.PageNumber, request.PageSize, request.Search, request.IsShared, request.Tags, cancellationToken)
            .ConfigureAwait(false);

        var dtoItems = result.Items
            .Select(_mapper.MapToPromptTemplateDto)
            .Select(dto => new PromptTemplateListDto(
                dto.Id,
                dto.UserId,
                dto.Title,
                dto.Command,
                dto.IsShared,
                dto.Tags,
                dto.UsageCount,
                dto.CreatedAt,
                dto.UpdatedAt))
            .ToList();

        return new PaginatedList<PromptTemplateListDto>(dtoItems, result.PageNumber, result.TotalPages, result.TotalCount);
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
