using System.Collections.Concurrent;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Agents.Services;
using nem.Contracts.Agents;
using nem.Mimir.Domain.ValueObjects;
using ContractSpecialistAgent = nem.Contracts.Agents.ISpecialistAgent;

namespace nem.Mimir.Application.Agents;

public sealed class AgentOrchestrator : IAgentOrchestrator
{
    private readonly AgentDispatcher _dispatcher;
    private readonly AgentCoordinator _coordinator;
    private readonly ITrajectoryRecorder _trajectoryRecorder;
    private readonly ConcurrentDictionary<string, AgentResult> _taskStatuses = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _taskCancellation = new(StringComparer.OrdinalIgnoreCase);

    public AgentOrchestrator(
        AgentDispatcher dispatcher,
        AgentCoordinator coordinator,
        ILlmService llmService,
        ITrajectoryRecorder trajectoryRecorder)
    {
        _dispatcher = dispatcher;
        _coordinator = coordinator;
        _trajectoryRecorder = trajectoryRecorder;
        _ = llmService;
    }

    public async Task<AgentResult> DispatchAsync(AgentTask task, CancellationToken cancellationToken = default)
    {
        var maxTurns = ResolveMaxTurns(task);
        var strategy = ResolveStrategy(task);
        var executionContext = new AgentExecutionContext(task, maxTurns);
        var sessionId = task.Context is not null
            && task.Context.TryGetValue("sessionId", out var contextSessionId)
            && !string.IsNullOrWhiteSpace(contextSessionId)
            ? contextSessionId
            : task.Id;

        _taskStatuses[task.Id] = new AgentResult(task.Id, "orchestrator", "Pending");

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _taskCancellation[task.Id] = linkedCts;

        var trajectoryId = await _trajectoryRecorder
            .StartRecordingAsync(sessionId, "orchestrator", linkedCts.Token)
            .ConfigureAwait(false);

        try
        {
            var candidateStart = DateTime.UtcNow;
            var candidates = strategy == AgentCoordinationStrategy.Sequential
                ? new[] { await _dispatcher.DispatchAsync(task, linkedCts.Token).ConfigureAwait(false) }
                : await _dispatcher.GetCandidatesAsync(task, linkedCts.Token).ConfigureAwait(false);

            await _trajectoryRecorder
                .RecordStepAsync(
                    trajectoryId,
                    TrajectoryStep.Create(
                        "SelectCandidates",
                        $"strategy={strategy}",
                        $"candidates={candidates.Count}",
                        DateTime.UtcNow - candidateStart,
                        true,
                        candidateStart),
                    linkedCts.Token)
                .ConfigureAwait(false);

            if (candidates.Count == 0)
            {
                var noAgent = new AgentResult(task.Id, "orchestrator", "Failed", "No specialist agents available for task.");
                _taskStatuses[task.Id] = noAgent;

                await _trajectoryRecorder
                    .CompleteRecordingAsync(trajectoryId, false, noAgent.Output, ct: linkedCts.Token)
                    .ConfigureAwait(false);

                return noAgent;
            }

            var executionStart = DateTime.UtcNow;
            var result = await _coordinator.ExecuteAsync(executionContext, candidates, strategy, linkedCts.Token).ConfigureAwait(false);

            var isSuccess = result.Status.Equals("Completed", StringComparison.OrdinalIgnoreCase)
                || result.Status.Equals("Success", StringComparison.OrdinalIgnoreCase);

            await _trajectoryRecorder
                .RecordStepAsync(
                    trajectoryId,
                    TrajectoryStep.Create(
                        "ExecuteTask",
                        task.Prompt,
                        result.Output,
                        DateTime.UtcNow - executionStart,
                        isSuccess,
                        executionStart),
                    linkedCts.Token)
                .ConfigureAwait(false);

            await _trajectoryRecorder
                .CompleteRecordingAsync(trajectoryId, isSuccess, isSuccess ? null : result.Output, ct: linkedCts.Token)
                .ConfigureAwait(false);

            _taskStatuses[task.Id] = result;
            return result;
        }
        catch (OperationCanceledException) when (linkedCts.IsCancellationRequested)
        {
            var canceled = new AgentResult(task.Id, "orchestrator", "Cancelled");
            _taskStatuses[task.Id] = canceled;

            await _trajectoryRecorder
                .CompleteRecordingAsync(trajectoryId, false, "Execution was cancelled.", ct: CancellationToken.None)
                .ConfigureAwait(false);

            return canceled;
        }
        catch (Exception ex)
        {
            var failed = new AgentResult(task.Id, "orchestrator", "Failed", ex.Message);
            _taskStatuses[task.Id] = failed;

            await _trajectoryRecorder
                .CompleteRecordingAsync(trajectoryId, false, ex.Message, ct: CancellationToken.None)
                .ConfigureAwait(false);

            return failed;
        }
        finally
        {
            if (_taskCancellation.TryRemove(task.Id, out var cts))
            {
                cts.Dispose();
            }
        }
    }

    public Task<ContractSpecialistAgent?> GetAgentAsync(string agentId, CancellationToken cancellationToken = default)
    {
        return _dispatcher.GetAgentAsync(agentId, cancellationToken);
    }

    public Task<IReadOnlyList<ContractSpecialistAgent>> ListAgentsAsync(CancellationToken cancellationToken = default)
    {
        return _dispatcher.ListAgentsAsync(cancellationToken);
    }

    public Task<AgentResult?> GetTaskStatusAsync(string taskId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _taskStatuses.TryGetValue(taskId, out var status);
        return Task.FromResult(status);
    }

    public Task CancelTaskAsync(string taskId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_taskCancellation.TryGetValue(taskId, out var cts))
        {
            cts.Cancel();
        }

        _taskStatuses[taskId] = new AgentResult(taskId, "orchestrator", "Cancelled");
        return Task.CompletedTask;
    }

    public Task RegisterAgentAsync(ContractSpecialistAgent agent, CancellationToken cancellationToken = default)
    {
        return _dispatcher.RegisterAsync(agent, cancellationToken);
    }

    public async Task<AgentResult> RequestAgentAsync(
        string agentId,
        AgentExecutionContext context,
        string prompt,
        CancellationToken cancellationToken = default)
    {
        var agent = await _dispatcher.GetAgentAsync(agentId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Agent '{agentId}' is not registered.");

        var childTask = context.Task with
        {
            Id = Guid.NewGuid().ToString("N"),
            Prompt = prompt,
            ParentTaskId = context.Task.Id,
        };

        var childContext = context.CreateChild(childTask);
        var result = await _coordinator.ExecuteAsync(
            childContext,
            new[] { agent },
            AgentCoordinationStrategy.Sequential,
            cancellationToken).ConfigureAwait(false);

        childContext.ToolResults[agent.Name] = result;
        return result;
    }

    private static int ResolveMaxTurns(AgentTask task)
    {
        if (task.Context is not null
            && task.Context.TryGetValue("maxTurns", out var maxTurnsRaw)
            && int.TryParse(maxTurnsRaw, out var maxTurns)
            && maxTurns > 0)
        {
            return maxTurns;
        }

        return 10;
    }

    private static AgentCoordinationStrategy ResolveStrategy(AgentTask task)
    {
        if (task.Context is null || !task.Context.TryGetValue("strategy", out var raw) || string.IsNullOrWhiteSpace(raw))
        {
            return AgentCoordinationStrategy.Sequential;
        }

        return raw.Trim().ToLowerInvariant() switch
        {
            "parallel" => AgentCoordinationStrategy.Parallel,
            "hierarchical" => AgentCoordinationStrategy.Hierarchical,
            _ => AgentCoordinationStrategy.Sequential,
        };
    }
}
