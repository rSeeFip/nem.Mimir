namespace nem.Mimir.Application.Conversations.Services;

public static class ConversationKnowledgeCollectionNaming
{
    public static string BuildName(Guid conversationId) => $"conversation-{conversationId:N}";
}
