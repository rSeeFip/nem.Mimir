namespace nem.Mimir.Application.Common.Models;

using nem.Mimir.Domain.ValueObjects;

public sealed record ConversationSearchResultDto(
    MessageId MessageId,
    ConversationId ConversationId,
    string ConversationTitle,
    string MessageSnippet,
    DateTimeOffset CreatedAt)
{
    public ConversationSearchResultDto(
        object messageId,
        object conversationId,
        string conversationTitle,
        string messageSnippet,
        DateTimeOffset createdAt)
        : this(CoerceMessageId(messageId), CoerceConversationId(conversationId), conversationTitle, messageSnippet, createdAt)
    {
    }

    private static MessageId CoerceMessageId(object value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value is MessageId typedId ? typedId : MessageId.Parse(value.ToString()!, null);
    }

    private static ConversationId CoerceConversationId(object value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value is ConversationId typedId ? typedId : ConversationId.Parse(value.ToString()!, null);
    }
}
