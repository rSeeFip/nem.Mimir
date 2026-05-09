using FluentValidation;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Models;

namespace nem.Mimir.Application.Knowledge.Queries;

public sealed record WebSearchQuery(string Query) : IQuery<IReadOnlyList<WebSearchResultDto>>;

public sealed class WebSearchQueryValidator : AbstractValidator<WebSearchQuery>
{
    public WebSearchQueryValidator()
    {
        RuleFor(x => x.Query)
            .NotEmpty().WithMessage("Search query is required.")
            .MaximumLength(1000).WithMessage("Search query must not exceed 1000 characters.");
    }
}

internal sealed class WebSearchQueryHandler
{
    private readonly ISearxngClient _searxngClient;

    public WebSearchQueryHandler(ISearxngClient searxngClient)
    {
        _searxngClient = searxngClient;
    }

    public async Task<IReadOnlyList<WebSearchResultDto>> Handle(WebSearchQuery request, CancellationToken cancellationToken)
    {
        return await _searxngClient.SearchAsync(request.Query, cancellationToken).ConfigureAwait(false);
    }
}
