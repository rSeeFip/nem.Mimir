namespace nem.Mimir.Infrastructure.Persistence.Repositories;

using Marten;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Domain.Entities;
using nem.Mimir.Domain.ValueObjects;

internal sealed class ConversationSearchService(IQuerySession session) : IConversationSearchService
{
    public Task<IReadOnlyList<ConversationSearchResultDto>> SearchAsync(
        object userId,
        string query,
        string? workspaceId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);
        var typedUserId = userId is UserId value ? value : UserId.Parse(userId.ToString()!, null);
        return SearchAsync(typedUserId, query, workspaceId, cancellationToken);
    }

    public async Task<IReadOnlyList<ConversationSearchResultDto>> SearchAsync(
        UserId userId,
        string query,
        string? workspaceId,
        CancellationToken cancellationToken = default)
    {
        _ = workspaceId;

        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        return await SearchWithFallbackAsync(userId, query.Trim(), cancellationToken).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<ConversationSearchResultDto>> SearchWithFallbackAsync(
        UserId userId,
        string query,
        CancellationToken cancellationToken)
    {
        var conversations = await session.Query<Conversation>()
            .Where(conversation => conversation.UserId == userId.Value)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (conversations.Count == 0)
        {
            return [];
        }

        var titlesByConversationId = conversations.ToDictionary(conversation => conversation.Id, conversation => conversation.Title);
        var conversationIds = titlesByConversationId.Keys.ToHashSet();

        var messages = await session.Query<Message>()
            .Where(message => conversationIds.Contains(message.ConversationId))
            .OrderByDescending(message => message.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return messages
            .Where(message => message.Content.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Select(message => new ConversationSearchResultDto(
                MessageId.From(message.Id),
                ConversationId.From(message.ConversationId),
                titlesByConversationId[message.ConversationId],
                BuildSnippet(message.Content, query),
                message.CreatedAt))
            .Take(50)
            .ToArray();
    }

    private static string BuildSnippet(string content, string query)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        var matchIndex = content.IndexOf(query, StringComparison.OrdinalIgnoreCase);
        if (matchIndex < 0)
        {
            return content.Length <= 180 ? content : $"{content[..180]}…";
        }

        var start = Math.Max(0, matchIndex - 48);
        var length = Math.Min(content.Length - start, Math.Max(query.Length + 96, 120));
        var snippet = content.Substring(start, length).Trim();
        if (start > 0)
        {
            snippet = $"…{snippet}";
        }

        if (start + length < content.Length)
        {
            snippet = $"{snippet}…";
        }

        return snippet;
    }
}
