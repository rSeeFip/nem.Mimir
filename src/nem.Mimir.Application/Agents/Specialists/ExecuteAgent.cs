using System.Diagnostics;
using Microsoft.Extensions.Logging;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Models;
using nem.Contracts.Agents;

namespace nem.Mimir.Application.Agents.Specialists;

/// <summary>
/// Tool execution agent with sandbox access.
/// Specialized in running code, invoking tools, and automating tasks.
/// Maintains an action log of all operations performed.
/// Has read-only source access plus sandbox execution — does NOT write to source files.
/// </summary>
public sealed class ExecuteAgent : ISpecialistAgent
{
    private const string AgentId = "execute";
    private const int TokenBudget = 8192;
    private const string DefaultModel = "gpt-4";

    private const string SystemPrompt =
        "You are an execution specialist with access to a sandboxed environment. " +
        "You can run code (Python, JavaScript), invoke tools, and automate tasks. " +
        "You have read-only access to source code but can execute code in an isolated sandbox. " +
        "Always explain what you're about to execute before running it. " +
        "Report results clearly, including any errors or unexpected output. " +
        "You must NOT modify source files directly — only execute within the sandbox.";

    private static readonly string[] HandledKeywords =
        ["run", "execute", "create", "modify", "build", "compile", "test", "automate", "script", "generate"];

    private readonly ILlmService _llmService;
    private readonly ISandboxService _sandboxService;
    private readonly ILogger<ExecuteAgent> _logger;
    private readonly List<ActionLogEntry> _actionLog = [];

    public ExecuteAgent(ILlmService llmService, ISandboxService sandboxService, ILogger<ExecuteAgent> logger)
    {
        _llmService = llmService ?? throw new ArgumentNullException(nameof(llmService));
        _sandboxService = sandboxService ?? throw new ArgumentNullException(nameof(sandboxService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public string Name => "Execute Agent";

    /// <inheritdoc />
    public string Description => "Execution specialist with sandbox access for running code and automating tasks.";

    /// <inheritdoc />
    public IReadOnlyList<AgentCapability> Capabilities { get; } =
        [AgentCapability.CodeExecution, AgentCapability.ToolInvocation, AgentCapability.DataProcessing];

    /// <summary>Maximum token budget for this agent.</summary>
    public int MaxTokenBudget => TokenBudget;

    /// <summary>Read-only view of all actions taken during execution.</summary>
    public IReadOnlyList<ActionLogEntry> ActionLog => _actionLog.AsReadOnly();

    /// <inheritdoc />
    public Task<bool> CanHandleAsync(AgentTask task, CancellationToken cancellationToken = default)
    {
        if (task.Type == AgentTaskType.Execute)
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
        _logger.LogInformation("ExecuteAgent processing task {TaskId}: {Prompt}",
            task.Id, task.Prompt[..Math.Min(task.Prompt.Length, 100)]);

        var stopwatch = Stopwatch.StartNew();

        LogAction("TaskStarted", $"Processing: {task.Prompt[..Math.Min(task.Prompt.Length, 200)]}");

        var messages = BuildMessages(task);
        var artifacts = new List<string>();

        try
        {
            // Step 1: Get LLM guidance on what to execute
            var response = await _llmService.SendMessageAsync(DefaultModel, messages, cancellationToken);
            LogAction("LlmResponse", "Received execution guidance from LLM");

            var totalTokens = response.TotalTokens;

            // Step 2: If the task context contains code to execute, run it in sandbox
            if (task.Context is not null && task.Context.TryGetValue("code", out var code))
            {
                var language = task.Context.TryGetValue("language", out var lang) ? lang : "python";

                LogAction("SandboxExecution", $"Executing {language} code in sandbox");

                var sandboxResult = await _sandboxService.ExecuteAsync(code, language, cancellationToken);

                LogAction("SandboxResult",
                    $"Exit code: {sandboxResult.ExitCode}, " +
                    $"Timed out: {sandboxResult.TimedOut}, " +
                    $"Duration: {sandboxResult.ExecutionTimeMs}ms");

                artifacts.Add($"sandbox-execution:{language}:exit-{sandboxResult.ExitCode}");

                // Append sandbox results to the output
                var output = $"{response.Content}\n\n--- Sandbox Execution Result ---\n" +
                             $"Language: {language}\n" +
                             $"Exit Code: {sandboxResult.ExitCode}\n" +
                             $"Timed Out: {sandboxResult.TimedOut}\n" +
                             $"Duration: {sandboxResult.ExecutionTimeMs}ms\n" +
                             $"Stdout:\n{sandboxResult.Stdout}\n" +
                             (string.IsNullOrEmpty(sandboxResult.Stderr) ? "" : $"Stderr:\n{sandboxResult.Stderr}\n");

                stopwatch.Stop();

                return new AgentResult(
                    TaskId: task.Id,
                    AgentId: AgentId,
                    Status: sandboxResult.ExitCode == 0 ? "Completed" : "CompletedWithErrors",
                    Output: output,
                    TokensUsed: Math.Min(totalTokens, TokenBudget),
                    ExecutionTimeMs: stopwatch.ElapsedMilliseconds,
                    Artifacts: artifacts);
            }

            stopwatch.Stop();

            LogAction("TaskCompleted", $"Completed in {stopwatch.ElapsedMilliseconds}ms");

            return new AgentResult(
                TaskId: task.Id,
                AgentId: AgentId,
                Status: "Completed",
                Output: response.Content,
                TokensUsed: Math.Min(totalTokens, TokenBudget),
                ExecutionTimeMs: stopwatch.ElapsedMilliseconds,
                Artifacts: artifacts.Count > 0 ? artifacts : null);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            LogAction("TaskFailed", $"Error: {ex.Message}");
            _logger.LogError(ex, "ExecuteAgent failed processing task {TaskId}", task.Id);

            return new AgentResult(
                TaskId: task.Id,
                AgentId: AgentId,
                Status: "Failed",
                Output: $"Error: {ex.Message}",
                ExecutionTimeMs: stopwatch.ElapsedMilliseconds);
        }
    }

    private void LogAction(string action, string details)
    {
        var entry = new ActionLogEntry(
            Timestamp: DateTimeOffset.UtcNow,
            Action: action,
            Details: details);

        _actionLog.Add(entry);

        _logger.LogDebug("ExecuteAgent action: {Action} — {Details}", action, details);
    }

    private static List<LlmMessage> BuildMessages(AgentTask task)
    {
        var messages = new List<LlmMessage>
        {
            new("system", SystemPrompt),
        };

        // Add context data as tool results
        if (task.Context is { Count: > 0 })
        {
            var toolContext = string.Join("\n", task.Context.Select(
                kvp => $"[{kvp.Key}]: {kvp.Value}"));
            messages.Add(new LlmMessage("system", $"Available tool results:\n{toolContext}"));
        }

        messages.Add(new LlmMessage("user", task.Prompt));
        return messages;
    }

    /// <summary>
    /// Represents an entry in the agent's action log.
    /// </summary>
    /// <param name="Timestamp">When the action occurred.</param>
    /// <param name="Action">The action type identifier.</param>
    /// <param name="Details">Human-readable details about the action.</param>
    public sealed record ActionLogEntry(
        DateTimeOffset Timestamp,
        string Action,
        string Details);
}
