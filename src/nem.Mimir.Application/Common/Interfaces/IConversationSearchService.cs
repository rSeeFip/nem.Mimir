namespace nem.Mimir.Application.Common.Interfaces;

using nem.Mimir.Application.Common.Models;
using nem.Mimir.Domain.ValueObjects;

public interface IConversationSearchService
{
    Task<IReadOnlyList<ConversationSearchResultDto>> SearchAsync(
        UserId userId,
        string query,
        string? workspaceId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ConversationSearchResultDto>> SearchAsync(
        object userId,
        string query,
        string? workspaceId,
        CancellationToken cancellationToken = default);
}
