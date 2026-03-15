using System.Diagnostics;
using Microsoft.Extensions.Logging;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Models;
using nem.Contracts.Agents;

namespace nem.Mimir.Application.Agents.Specialists;

/// <summary>
/// Deep reasoning and analysis agent.
/// Specialized in debugging, architecture review, code review, and analytical reasoning tasks.
/// Uses a larger token budget (16K) to support complex, multi-step reasoning.
/// </summary>
public sealed class AnalyzeAgent : ISpecialistAgent
{
    private const string AgentId = "analyze";
    private const int TokenBudget = 16384;
    private const string DefaultModel = "gpt-4";

    private const string SystemPrompt =
        "You are a deep analytical reasoning specialist. " +
        "Your expertise lies in thorough analysis, debugging complex issues, " +
        "reviewing architecture decisions, and performing detailed code reviews. " +
        "When analyzing, break down problems step by step. " +
        "Identify root causes, not just symptoms. " +
        "Consider edge cases, performance implications, and security concerns. " +
        "Provide actionable recommendations with clear reasoning.";

    private static readonly string[] HandledKeywords =
        ["analyze", "debug", "review", "why does", "root cause", "investigate", "examine", "diagnose", "architecture", "performance"];

    private readonly ILlmService _llmService;
    private readonly ILogger<AnalyzeAgent> _logger;

    public AnalyzeAgent(ILlmService llmService, ILogger<AnalyzeAgent> logger)
    {
        _llmService = llmService ?? throw new ArgumentNullException(nameof(llmService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public string Name => "Analyze Agent";

    /// <inheritdoc />
    public string Description => "Deep analytical reasoning specialist for debugging, code review, and architecture analysis.";

    /// <inheritdoc />
    public IReadOnlyList<AgentCapability> Capabilities { get; } =
        [AgentCapability.DeepAnalysis, AgentCapability.CodeExploration, AgentCapability.KnowledgeRetrieval];

    /// <summary>Maximum token budget for this agent.</summary>
    public int MaxTokenBudget => TokenBudget;

    /// <inheritdoc />
    public Task<bool> CanHandleAsync(AgentTask task, CancellationToken cancellationToken = default)
    {
        if (task.Type == AgentTaskType.Analyze)
        {
            return Task.FromResult(true);
        }

        if (task.Type == AgentTaskType.Custom)
        {
            var prompt = task.Prompt.ToLowerInvariant();
            var canHandle = Array.Exists(HandledKeywords, keyword => prompt.Contains(keyword, StringComparison.Ordinal));
            return Task.FromResult(canHandle);
        }

        return Task.FromResult(false);
    }

    /// <inheritdoc />
    public async Task<AgentResult> ExecuteAsync(AgentTask task, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("AnalyzeAgent processing task {TaskId}: {Prompt}",
            task.Id, task.Prompt[..Math.Min(task.Prompt.Length, 100)]);

        var stopwatch = Stopwatch.StartNew();

        var messages = BuildMessages(task);

        try
        {
            var response = await _llmService.SendMessageAsync(DefaultModel, messages, cancellationToken);

            stopwatch.Stop();

            _logger.LogInformation(
                "AnalyzeAgent completed task {TaskId} in {ElapsedMs}ms, {Tokens} tokens used",
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
            _logger.LogError(ex, "AnalyzeAgent failed processing task {TaskId}", task.Id);

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
        var messages = new List<LlmMessage>
        {
            new("system", SystemPrompt),
        };

        // Add context data as additional system context for deeper analysis
        if (task.Context is { Count: > 0 })
        {
            var contextInfo = string.Join("\n", task.Context.Select(
                kvp => $"[{kvp.Key}]: {kvp.Value}"));
            messages.Add(new LlmMessage("system", $"Additional context:\n{contextInfo}"));
        }

        messages.Add(new LlmMessage("user", task.Prompt));
        return messages;
    }
}
