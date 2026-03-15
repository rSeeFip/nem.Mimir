namespace nem.Mimir.Application.Tasks;

using nem.Contracts.Agents;

/// <summary>
/// Runtime for dispatching, tracking, and managing the lifecycle of background agent tasks.
/// </summary>
public interface IBackgroundTaskRuntime
{
    /// <summary>Records a newly submitted task for the specified agent.</summary>
    Task RecordSubmittedAsync(AgentTask task, string agentId, CancellationToken cancellationToken = default);

    /// <summary>Transitions the task to running state.</summary>
    Task MarkRunningAsync(string taskId, CancellationToken cancellationToken = default);

    /// <summary>Records successful task completion with its result.</summary>
    Task MarkCompletedAsync(AgentResult result, CancellationToken cancellationToken = default);

    /// <summary>Records task failure with an error message.</summary>
    Task MarkFailedAsync(string taskId, string error, CancellationToken cancellationToken = default);

    /// <summary>Records task cancellation with an optional reason.</summary>
    Task MarkCancelledAsync(string taskId, string? error = null, CancellationToken cancellationToken = default);

    /// <summary>Dispatches a task to the appropriate agent and awaits its result.</summary>
    Task<AgentResult> DispatchAsync(AgentTask task, CancellationToken cancellationToken = default);

    /// <summary>Cancels an in-progress task execution.</summary>
    Task CancelExecutionAsync(string taskId, CancellationToken cancellationToken = default);
}
