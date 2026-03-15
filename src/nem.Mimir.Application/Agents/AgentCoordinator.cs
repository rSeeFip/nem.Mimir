using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Models;
using nem.Contracts.Agents;
using ContractSpecialistAgent = nem.Contracts.Agents.ISpecialistAgent;

namespace nem.Mimir.Application.Agents;

public enum AgentCoordinationStrategy
{
    Sequential,
    Parallel,
    Hierarchical,
}

public sealed class AgentCoordinator
{
    private static readonly TimeSpan PerTurnTimeout = TimeSpan.FromSeconds(30);
    private readonly ILlmService _llmService;
    private readonly string _coordinationModel;

    public AgentCoordinator(ILlmService llmService, string coordinationModel = "phi-4-mini")
    {
        _llmService = llmService;
        _coordinationModel = coordinationModel;
    }

    public async Task<AgentResult> ExecuteAsync(
        AgentExecutionContext context,
        IReadOnlyList<ContractSpecialistAgent> agents,
        AgentCoordinationStrategy strategy,
        CancellationToken cancellationToken = default)
    {
        if (agents.Count == 0)
        {
            throw new InvalidOperationException("At least one specialist agent is required.");
        }

        return strategy switch
        {
            AgentCoordinationStrategy.Sequential => await ExecuteSequentialAsync(context, agents, cancellationToken).ConfigureAwait(false),
            AgentCoordinationStrategy.Parallel => await ExecuteParallelAsync(context, agents, cancellationToken).ConfigureAwait(false),
            AgentCoordinationStrategy.Hierarchical => await ExecuteHierarchicalAsync(context, agents, cancellationToken).ConfigureAwait(false),
            _ => throw new ArgumentOutOfRangeException(nameof(strategy), strategy, null),
        };
    }

    private async Task<AgentResult> ExecuteSequentialAsync(
        AgentExecutionContext context,
        IReadOnlyList<ContractSpecialistAgent> agents,
        CancellationToken cancellationToken)
    {
        AgentResult? last = null;

        foreach (var agent in agents)
        {
            var result = await ExecuteTurnAsync(agent, context.Task, context, cancellationToken).ConfigureAwait(false);
            context.ToolResults[agent.Name] = result;
            last = result;

            if (!IsSuccess(result))
            {
                return result;
            }
        }

        return last ?? new AgentResult(context.Task.Id, agents[0].Name, "Completed");
    }

    private async Task<AgentResult> ExecuteParallelAsync(
        AgentExecutionContext context,
        IReadOnlyList<ContractSpecialistAgent> agents,
        CancellationToken cancellationToken)
    {
        var runs = agents.Select(agent => ExecuteTurnAsync(agent, context.Task, context, cancellationToken));
        var results = await Task.WhenAll(runs).ConfigureAwait(false);

        for (var i = 0; i < agents.Count; i++)
        {
            context.ToolResults[agents[i].Name] = results[i];
        }

        var firstFailure = results.FirstOrDefault(result => !IsSuccess(result));
        if (firstFailure is not null)
        {
            return firstFailure;
        }

        var mergedOutput = string.Join(Environment.NewLine, results.Where(x => !string.IsNullOrWhiteSpace(x.Output)).Select(x => x.Output));
        var mergedArtifacts = results
            .SelectMany(x => x.Artifacts ?? (IReadOnlyList<string>)Array.Empty<string>())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new AgentResult(
            context.Task.Id,
            "parallel-coordinator",
            "Completed",
            mergedOutput,
            results.Sum(x => x.TokensUsed),
            results.Sum(x => x.ExecutionTimeMs),
            mergedArtifacts);
    }

    private async Task<AgentResult> ExecuteHierarchicalAsync(
        AgentExecutionContext context,
        IReadOnlyList<ContractSpecialistAgent> agents,
        CancellationToken cancellationToken)
    {
        var supervisor = agents[0];
        var supervisorResult = await ExecuteTurnAsync(supervisor, context.Task, context, cancellationToken).ConfigureAwait(false);
        context.ToolResults[supervisor.Name] = supervisorResult;

        if (!IsSuccess(supervisorResult))
        {
            return supervisorResult;
        }

        if (agents.Count == 1)
        {
            return supervisorResult;
        }

        var childContext = context.CreateChild(context.Task with { ParentTaskId = context.Task.Id });
        var childResults = new List<AgentResult>();

        foreach (var agent in agents.Skip(1))
        {
            var result = await ExecuteTurnAsync(agent, childContext.Task, childContext, cancellationToken).ConfigureAwait(false);
            childContext.ToolResults[agent.Name] = result;
            childResults.Add(result);
        }

        var synthesisMessages = new List<LlmMessage>
        {
            new("system", "Synthesize multi-agent outputs into one concise response."),
            new("user", BuildSynthesisPrompt(context.Task.Prompt, supervisorResult, childResults)),
        };

        var synthesis = await _llmService.SendMessageAsync(_coordinationModel, synthesisMessages, cancellationToken).ConfigureAwait(false);

        return new AgentResult(
            context.Task.Id,
            supervisor.Name,
            "Completed",
            synthesis.Content,
            supervisorResult.TokensUsed + childResults.Sum(x => x.TokensUsed) + synthesis.TotalTokens,
            supervisorResult.ExecutionTimeMs + childResults.Sum(x => x.ExecutionTimeMs),
            childResults
                .SelectMany(x => x.Artifacts ?? (IReadOnlyList<string>)Array.Empty<string>())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList());
    }

    private static async Task<AgentResult> ExecuteTurnAsync(
        ContractSpecialistAgent agent,
        AgentTask task,
        AgentExecutionContext context,
        CancellationToken cancellationToken)
    {
        context.IncrementTurn();

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(PerTurnTimeout);

        var startedAt = DateTimeOffset.UtcNow;

        try
        {
            var result = await agent.ExecuteAsync(task, timeoutCts.Token).ConfigureAwait(false);
            return result with
            {
                ExecutionTimeMs = EnsureExecutionTime(result.ExecutionTimeMs, startedAt),
            };
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            return new AgentResult(task.Id, agent.Name, "TimedOut", null, 0, (long)PerTurnTimeout.TotalMilliseconds, Array.Empty<string>());
        }
        catch (Exception ex)
        {
            var elapsed = (long)(DateTimeOffset.UtcNow - startedAt).TotalMilliseconds;
            return new AgentResult(task.Id, agent.Name, "Failed", ex.Message, 0, elapsed, Array.Empty<string>());
        }
    }

    private static string BuildSynthesisPrompt(string originalPrompt, AgentResult supervisorResult, IReadOnlyList<AgentResult> childResults)
    {
        var childSummary = string.Join(
            Environment.NewLine,
            childResults.Select(x => $"- {x.AgentId} ({x.Status}): {x.Output}"));

        return $"Prompt: {originalPrompt}{Environment.NewLine}Supervisor: {supervisorResult.Output}{Environment.NewLine}Delegates:{Environment.NewLine}{childSummary}";
    }

    private static long EnsureExecutionTime(long reportedExecutionTime, DateTimeOffset startedAt)
    {
        if (reportedExecutionTime > 0)
        {
            return reportedExecutionTime;
        }

        return (long)(DateTimeOffset.UtcNow - startedAt).TotalMilliseconds;
    }

    private static bool IsSuccess(AgentResult result)
    {
        return result.Status.Equals("Completed", StringComparison.OrdinalIgnoreCase)
               || result.Status.Equals("Success", StringComparison.OrdinalIgnoreCase);
    }
}
