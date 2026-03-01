using Microsoft.Extensions.Logging;
using Mimir.Application.Common.Interfaces;
using Mimir.Application.Common.Models;
using Mimir.Domain.Entities;

namespace Mimir.Infrastructure.Services;

/// <summary>
/// Manages LLM context window construction with DB-driven system prompt resolution,
/// token estimation, and history truncation.
/// </summary>
internal sealed class ContextWindowService(
    ISystemPromptRepository systemPromptRepository,
    ILogger<ContextWindowService> logger) : IContextWindowService
{
    internal const string FallbackSystemPrompt = "You are Mimir, a helpful AI assistant.";
    private const string DefaultModel = "phi-4-mini";
    private const int DefaultTokenLimit = 16_384;

    private static readonly Dictionary<string, int> ModelTokenLimits = new(StringComparer.OrdinalIgnoreCase)
    {
        ["phi-4-mini"] = 16_384,
        ["qwen-2.5-72b"] = 131_072,
        ["qwen-2.5-coder-32b"] = 131_072,
    };

    /// <inheritdoc />
    public async Task<IReadOnlyList<LlmMessage>> BuildLlmMessagesAsync(
        Conversation conversation,
        string newUserContent,
        string? model,
        CancellationToken cancellationToken = default)
    {
        var selectedModel = model ?? DefaultModel;
        var tokenLimit = GetTokenLimit(selectedModel);

        // Resolve system prompt: DB default → fallback constant
        var defaultPrompt = await systemPromptRepository.GetDefaultAsync(cancellationToken);
        var systemPromptText = defaultPrompt?.Template ?? FallbackSystemPrompt;

        if (defaultPrompt is not null)
        {
            logger.LogDebug("Using DB system prompt '{Name}' (Id: {Id})", defaultPrompt.Name, defaultPrompt.Id);
        }

        var systemMessage = new LlmMessage("system", systemPromptText);
        var newUserMessage = new LlmMessage("user", newUserContent);

        // Map existing history messages (ordered by creation time)
        var historyMessages = conversation.Messages
            .OrderBy(m => m.CreatedAt)
            .Select(m => new LlmMessage(m.Role.ToString().ToLowerInvariant(), m.Content))
            .ToList();

        // Calculate base token usage: system prompt + new user message
        var systemTokens = EstimateTokenCount(systemMessage.Content);
        var newUserTokens = EstimateTokenCount(newUserMessage.Content);
        var baseTokens = systemTokens + newUserTokens;

        // Calculate total history tokens
        var historyTokens = historyMessages
            .Select(m => EstimateTokenCount(m.Content))
            .ToList();

        var totalTokens = baseTokens + historyTokens.Sum();

        // Remove oldest non-system messages (from the front) until within limit
        var startIndex = 0;
        while (totalTokens > tokenLimit && startIndex < historyMessages.Count)
        {
            totalTokens -= historyTokens[startIndex];
            startIndex++;
        }

        // Build final message list
        var result = new List<LlmMessage> { systemMessage };
        result.AddRange(historyMessages.Skip(startIndex));
        result.Add(newUserMessage);

        return result;
    }

    /// <inheritdoc />
    public int GetTokenLimit(string? model) =>
        model is not null && ModelTokenLimits.TryGetValue(model, out var limit)
            ? limit
            : DefaultTokenLimit;

    /// <inheritdoc />
    public int EstimateTokenCount(string text) =>
        string.IsNullOrEmpty(text) ? 0 : (text.Length + 3) / 4;
}
