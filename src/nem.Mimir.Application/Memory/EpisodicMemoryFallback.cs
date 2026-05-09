using nem.Mimir.Domain.Entities;
using nem.Mimir.Domain.Enums;

namespace nem.Mimir.Application.Services.Memory;

internal static class EpisodicMemoryFallback
{
    public static string BuildSummary(Conversation conversation, IReadOnlyList<Message> messages)
    {
        var latestUserMessage = messages
            .Where(m => m.Role == MessageRole.User)
            .Select(m => m.Content)
            .LastOrDefault(m => !string.IsNullOrWhiteSpace(m));

        if (!string.IsNullOrWhiteSpace(latestUserMessage))
        {
            return $"Conversation '{conversation.Title}' focused on: {latestUserMessage.Trim()}";
        }

        return $"Conversation '{conversation.Title}' contained {messages.Count} messages.";
    }

    public static List<string> ExtractTopics(IReadOnlyList<Message> messages)
    {
        return messages
            .SelectMany(m => m.Content.Split([' ', '\t', '\r', '\n', ',', '.', ';', ':', '!', '?'], StringSplitOptions.RemoveEmptyEntries))
            .Where(x => x.Length >= 4)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToList();
    }

    public static int EstimateTokenCount(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        return (text.Length + 3) / 4;
    }
}
