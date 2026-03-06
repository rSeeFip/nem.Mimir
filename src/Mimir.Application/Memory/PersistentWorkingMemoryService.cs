using Microsoft.Extensions.Logging;
using Mimir.Application.Common.Interfaces;
using Mimir.Application.Common.Models;
using Mimir.Domain.Entities;
using Mimir.Domain.Enums;
using nem.Contracts.Memory;

namespace Mimir.Application.Services.Memory;

public class PersistentWorkingMemoryService : IWorkingMemory
{
    private const string SummaryMarker = "[working-memory-summary]";
    private const string ClearedMarker = "[working-memory-cleared]";

    private readonly IConversationRepository _conversationRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILlmService _llmService;
    private readonly WorkingMemoryOptions _options;
    private readonly ILogger _logger;

    public PersistentWorkingMemoryService(
        IConversationRepository conversationRepository,
        IUnitOfWork unitOfWork,
        ILlmService llmService,
        WorkingMemoryOptions options,
        ILogger logger)
    {
        _conversationRepository = conversationRepository;
        _unitOfWork = unitOfWork;
        _llmService = llmService;
        _options = options;
        _logger = logger;
    }

    public async Task AddMessageAsync(string conversationId, ConversationMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);
        ArgumentNullException.ThrowIfNull(message);

        var conversationGuid = ParseConversationId(conversationId);
        var conversation = await _conversationRepository
            .GetWithMessagesAsync(conversationGuid, cancellationToken)
            .ConfigureAwait(false);

        if (conversation is null)
        {
            throw new InvalidOperationException($"Conversation '{conversationId}' not found.");
        }

        var role = ParseRole(message.Role);
        var entityMessage = conversation.AddMessage(role, message.Content);
        entityMessage.SetTokenCount(ExtractTokenCount(message));

        await _conversationRepository.UpdateAsync(conversation, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await SummarizeIfOverflowAsync(conversation, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ConversationMessage>> GetRecentAsync(string conversationId, int count = 20, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);

        if (count <= 0)
        {
            return Array.Empty<ConversationMessage>();
        }

        var conversation = await LoadConversationAsync(conversationId, cancellationToken).ConfigureAwait(false);
        var effective = GetEffectiveMessages(conversation);

        return effective
            .OrderByDescending(m => m.CreatedAt)
            .Take(count)
            .Select(MapToContract)
            .ToList();
    }

    public async Task<IReadOnlyList<ConversationMessage>> GetWindowAsync(string conversationId, int maxTokens, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);

        if (maxTokens <= 0)
        {
            return Array.Empty<ConversationMessage>();
        }

        var conversation = await LoadConversationAsync(conversationId, cancellationToken).ConfigureAwait(false);
        var descending = GetEffectiveMessages(conversation)
            .OrderByDescending(m => m.CreatedAt)
            .ToList();

        var selected = new List<Message>(descending.Count);
        var totalTokens = 0;

        foreach (var item in descending)
        {
            var tokens = GetMessageTokenCount(item);

            if (totalTokens + tokens > maxTokens)
            {
                break;
            }

            selected.Add(item);
            totalTokens += tokens;
        }

        return selected
            .OrderBy(m => m.CreatedAt)
            .Select(MapToContract)
            .ToList();
    }

    public async Task ClearAsync(string conversationId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);

        var conversation = await LoadConversationAsync(conversationId, cancellationToken).ConfigureAwait(false);
        var clearMessage = conversation.AddMessage(MessageRole.System, $"{ClearedMarker} {DateTimeOffset.UtcNow:O}");
        clearMessage.SetTokenCount(1);

        await _conversationRepository.UpdateAsync(conversation, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> GetTokenCountAsync(string conversationId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);

        var conversation = await LoadConversationAsync(conversationId, cancellationToken).ConfigureAwait(false);
        return GetEffectiveMessages(conversation).Sum(GetMessageTokenCount);
    }

    private async Task SummarizeIfOverflowAsync(Conversation conversation, CancellationToken cancellationToken)
    {
        var options = _options;
        var effectiveMessages = GetEffectiveMessages(conversation)
            .OrderBy(m => m.CreatedAt)
            .ToList();

        var totalTokens = effectiveMessages.Sum(GetMessageTokenCount);
        if (totalTokens <= options.MaxTokenWindow)
        {
            return;
        }

        var target = Math.Min(options.SummarizationThreshold, options.MaxTokenWindow);
        var toSummarize = new List<Message>();
        var summarizedTokens = 0;

        var maxSummarizable = Math.Max(0, effectiveMessages.Count - 1);

        foreach (var item in effectiveMessages.Take(maxSummarizable))
        {
            if (totalTokens - summarizedTokens <= target)
            {
                break;
            }

            toSummarize.Add(item);
            summarizedTokens += GetMessageTokenCount(item);
        }

        if (toSummarize.Count == 0)
        {
            return;
        }

        var summary = await CreateSummaryAsync(toSummarize, options.SummarizationModel, cancellationToken).ConfigureAwait(false);
        var cutoff = toSummarize[^1].CreatedAt;
        var summaryMessage = conversation.AddMessage(
            MessageRole.System,
            $"{SummaryMarker} {cutoff:O}\n{summary}",
            options.SummarizationModel);
        summaryMessage.SetTokenCount(EstimateTokenCount(summary));

        _logger.LogInformation(
            "Working memory summarized for conversation {ConversationId}. SummarizedCount={SummarizedCount}, TotalTokensBefore={TotalTokensBefore}",
            conversation.Id,
            toSummarize.Count,
            totalTokens);

        await _conversationRepository.UpdateAsync(conversation, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<string> CreateSummaryAsync(IReadOnlyList<Message> messages, string model, CancellationToken cancellationToken)
    {
        var transcript = string.Join(
            Environment.NewLine,
            messages.Select(m => $"[{m.Role}] {m.Content}"));

        var prompt = "Summarize the following conversation history into concise key facts and user intent for future context retention.";
        var response = await _llmService.SendMessageAsync(
            model,
            new[]
            {
                new LlmMessage("system", prompt),
                new LlmMessage("user", transcript)
            },
            cancellationToken).ConfigureAwait(false);

        return string.IsNullOrWhiteSpace(response.Content)
            ? "Summary unavailable."
            : response.Content.Trim();
    }

    private async Task<Conversation> LoadConversationAsync(string conversationId, CancellationToken cancellationToken)
    {
        var conversationGuid = ParseConversationId(conversationId);
        var conversation = await _conversationRepository
            .GetWithMessagesAsync(conversationGuid, cancellationToken)
            .ConfigureAwait(false);

        if (conversation is null)
        {
            throw new InvalidOperationException($"Conversation '{conversationId}' not found.");
        }

        return conversation;
    }

    private static Guid ParseConversationId(string conversationId)
    {
        if (!Guid.TryParse(conversationId, out var parsed))
        {
            throw new ArgumentException("Conversation ID must be a valid GUID.", nameof(conversationId));
        }

        return parsed;
    }

    private static MessageRole ParseRole(string role)
    {
        if (Enum.TryParse<MessageRole>(role, true, out var parsed))
        {
            return parsed;
        }

        return MessageRole.User;
    }

    private static ConversationMessage MapToContract(Message message)
    {
        var metadata = new Dictionary<string, string>
        {
            ["tokenCount"] = (message.TokenCount ?? 0).ToString(),
            ["messageId"] = message.Id.ToString()
        };

        if (!string.IsNullOrWhiteSpace(message.Model))
        {
            metadata["model"] = message.Model;
        }

        return new ConversationMessage(
            Role: message.Role.ToString().ToLowerInvariant(),
            Content: message.Content,
            Timestamp: message.CreatedAt,
            Metadata: metadata);
    }

    private static bool IsSummaryMessage(Message message) =>
        message.Role == MessageRole.System
        && message.Content.StartsWith(SummaryMarker, StringComparison.OrdinalIgnoreCase);

    private static bool IsClearedMessage(Message message) =>
        message.Role == MessageRole.System
        && message.Content.StartsWith(ClearedMarker, StringComparison.OrdinalIgnoreCase);

    private List<Message> GetEffectiveMessages(Conversation conversation)
    {
        var ordered = conversation.Messages.OrderBy(m => m.CreatedAt).ToList();
        if (ordered.Count == 0)
        {
            return new List<Message>();
        }

        var afterClear = ordered;
        var clearIndex = ordered.FindLastIndex(IsClearedMessage);
        if (clearIndex >= 0)
        {
            afterClear = ordered.Skip(clearIndex + 1).ToList();
        }

        if (afterClear.Count == 0)
        {
            return new List<Message>();
        }

        var latestSummary = afterClear.LastOrDefault(IsSummaryMessage);
        if (latestSummary is null)
        {
            return afterClear;
        }

        if (!TryReadSummaryCutoff(latestSummary.Content, out var cutoff))
        {
            var withFallback = new List<Message> { latestSummary };
            withFallback.AddRange(afterClear.Where(m => m.CreatedAt > latestSummary.CreatedAt && !IsSummaryMessage(m)));
            return withFallback;
        }

        var recentSinceCutoff = afterClear
            .Where(m => m.CreatedAt > cutoff && !IsSummaryMessage(m))
            .ToList();

        var effective = new List<Message> { latestSummary };
        effective.AddRange(recentSinceCutoff);
        return effective;
    }

    private int GetMessageTokenCount(Message message)
    {
        return message.TokenCount ?? EstimateTokenCount(message.Content);
    }

    private static int ExtractTokenCount(ConversationMessage message)
    {
        if (message.Metadata is not null
            && message.Metadata.TryGetValue("tokenCount", out var tokenValue)
            && int.TryParse(tokenValue, out var tokenCount)
            && tokenCount >= 0)
        {
            return tokenCount;
        }

        return EstimateTokenCount(message.Content);
    }

    private static int EstimateTokenCount(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        return (text.Length + 3) / 4;
    }

    private static bool TryReadSummaryCutoff(string content, out DateTimeOffset cutoff)
    {
        cutoff = default;

        if (!content.StartsWith(SummaryMarker, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var remainder = content[SummaryMarker.Length..].TrimStart();
        var newlineIndex = remainder.IndexOf('\n');
        var timestamp = newlineIndex >= 0 ? remainder[..newlineIndex].Trim() : remainder.Trim();

        return DateTimeOffset.TryParse(timestamp, out cutoff);
    }
}
