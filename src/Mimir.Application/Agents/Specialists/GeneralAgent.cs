using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Mimir.Application.Common.Interfaces;
using Mimir.Application.Common.Models;
using nem.Contracts.Agents;

namespace Mimir.Application.Agents.Specialists;

/// <summary>
/// Conversational agent for general-purpose interactions.
/// Handles greetings, explanations, summarizations, and friendly conversation.
/// No tool access — purely LLM-driven responses.
/// </summary>
public sealed class GeneralAgent : ISpecialistAgent
{
    private const string AgentId = "general";
    private const int TokenBudget = 4096;
    private const string DefaultModel = "gpt-4";

    private const string SystemPrompt =
        "You are a helpful, friendly general-purpose assistant. " +
        "You excel at conversational interactions, providing clear explanations, " +
        "summarizing information, and answering general questions. " +
        "Be concise and helpful. You do not have access to any tools — " +
        "respond purely from your training knowledge.";

    private static readonly string[] HandledKeywords =
        ["hello", "hi", "hey", "explain", "what is", "summarize", "summary", "tell me", "help", "thanks", "thank you"];

    private readonly ILlmService _llmService;
    private readonly ILogger<GeneralAgent> _logger;

    public GeneralAgent(ILlmService llmService, ILogger<GeneralAgent> logger)
    {
        _llmService = llmService ?? throw new ArgumentNullException(nameof(llmService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public string Name => "General Assistant";

    /// <inheritdoc />
    public string Description => "Conversational agent for greetings, explanations, and general questions.";

    /// <inheritdoc />
    public IReadOnlyList<AgentCapability> Capabilities { get; } =
        [AgentCapability.KnowledgeRetrieval];

    /// <summary>Maximum token budget for this agent.</summary>
    public int MaxTokenBudget => TokenBudget;

    /// <inheritdoc />
    public Task<bool> CanHandleAsync(AgentTask task, CancellationToken cancellationToken = default)
    {
        if (task.Type == AgentTaskType.Custom)
        {
            var prompt = task.Prompt.ToLowerInvariant();
            var canHandle = Array.Exists(HandledKeywords, keyword => prompt.Contains(keyword, StringComparison.Ordinal));
            return Task.FromResult(canHandle);
        }

        // General agent is a fallback — it can handle Research-type tasks
        return Task.FromResult(task.Type == AgentTaskType.Research);
    }

    /// <inheritdoc />
    public async Task<AgentResult> ExecuteAsync(AgentTask task, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("GeneralAgent processing task {TaskId}: {Prompt}",
            task.Id, task.Prompt[..Math.Min(task.Prompt.Length, 100)]);

        var stopwatch = Stopwatch.StartNew();

        var messages = BuildMessages(task);

        try
        {
            var response = await _llmService.SendMessageAsync(DefaultModel, messages, cancellationToken);

            stopwatch.Stop();

            _logger.LogInformation(
                "GeneralAgent completed task {TaskId} in {ElapsedMs}ms, {Tokens} tokens used",
                task.Id, stopwatch.ElapsedMilliseconds, response.TotalTokens);

            return new AgentResult(
                TaskId: task.Id,
                AgentId: AgentId,
                Status: "Completed",
                Output: response.Content,
                TokensUsed: Math.Min(response.TotalTokens, TokenBudget),
                ExecutionTimeMs: stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "GeneralAgent failed processing task {TaskId}", task.Id);

            return new AgentResult(
                TaskId: task.Id,
                AgentId: AgentId,
                Status: "Failed",
                Output: $"Error: {ex.Message}",
                ExecutionTimeMs: stopwatch.ElapsedMilliseconds);
        }
    }

    private static List<LlmMessage> BuildMessages(AgentTask task)
    {
        return
        [
            new LlmMessage("system", SystemPrompt),
            new LlmMessage("user", task.Prompt),
        ];
    }
}
