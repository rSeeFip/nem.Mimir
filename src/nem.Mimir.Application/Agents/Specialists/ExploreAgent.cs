using System.Diagnostics;
using Microsoft.Extensions.Logging;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Models;
using nem.Contracts.Agents;

namespace nem.Mimir.Application.Agents.Specialists;

/// <summary>
/// Read-only search and navigation agent.
/// Specialized in exploring information, searching codebases, reading files, and looking up documentation.
/// Does not modify any data — purely read-only operations.
/// </summary>
public sealed class ExploreAgent : ISpecialistAgent
{
    private const string AgentId = "explore";
    private const int TokenBudget = 8192;
    private const string DefaultModel = "gpt-4";

    private const string SystemPrompt =
        "You are a specialized exploration and search assistant. " +
        "Your expertise is in navigating codebases, finding relevant files, " +
        "reading documentation, and discovering patterns. " +
        "You have read-only access — you never modify, create, or delete anything. " +
        "Present findings in a clear, structured format with file paths and line references where applicable.";

    private static readonly string[] HandledKeywords =
        ["find", "where is", "show me", "search", "look for", "locate", "list", "what files", "navigate", "browse"];

    private readonly ILlmService _llmService;
    private readonly ILogger<ExploreAgent> _logger;

    public ExploreAgent(ILlmService llmService, ILogger<ExploreAgent> logger)
    {
        _llmService = llmService ?? throw new ArgumentNullException(nameof(llmService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public string Name => "Explore Agent";

    /// <inheritdoc />
    public string Description => "Read-only search and navigation agent for exploring codebases and documentation.";

    /// <inheritdoc />
    public IReadOnlyList<AgentCapability> Capabilities { get; } =
        [AgentCapability.CodeExploration, AgentCapability.KnowledgeRetrieval, AgentCapability.WebResearch];

    /// <summary>Maximum token budget for this agent.</summary>
    public int MaxTokenBudget => TokenBudget;

    /// <inheritdoc />
    public Task<bool> CanHandleAsync(AgentTask task, CancellationToken cancellationToken = default)
    {
        if (task.Type == AgentTaskType.Explore)
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
        _logger.LogInformation("ExploreAgent processing task {TaskId}: {Prompt}",
            task.Id, task.Prompt[..Math.Min(task.Prompt.Length, 100)]);

        var stopwatch = Stopwatch.StartNew();

        var messages = BuildMessages(task);

        try
        {
            var response = await _llmService.SendMessageAsync(DefaultModel, messages, cancellationToken);

            stopwatch.Stop();

            _logger.LogInformation(
                "ExploreAgent completed task {TaskId} in {ElapsedMs}ms, {Tokens} tokens used",
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
            _logger.LogError(ex, "ExploreAgent failed processing task {TaskId}", task.Id);

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
