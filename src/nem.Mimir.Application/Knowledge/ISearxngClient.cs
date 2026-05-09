using nem.Mimir.Application.Common.Models;

namespace nem.Mimir.Application.Knowledge;

public interface ISearxngClient
{
    Task<IReadOnlyList<WebSearchResultDto>> SearchAsync(string query, CancellationToken ct = default);
}
