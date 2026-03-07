namespace Mimir.Application.Tasks;

using nem.Contracts.Agents;

/// <summary>
/// Persistence abstraction for tracking background task lifecycle state transitions.
/// </summary>
public interface IBackgroundTaskStateStore
{
    Task RecordSubmittedAsync(AgentTask task, string agentId, CancellationToken cancellationToken = default);

    Task MarkRunningAsync(string taskId, CancellationToken cancellationToken = default);

    Task MarkCompletedAsync(AgentResult result, CancellationToken cancellationToken = default);

    Task MarkFailedAsync(string taskId, string error, CancellationToken cancellationToken = default);

    Task MarkCancelledAsync(string taskId, string? error = null, CancellationToken cancellationToken = default);
}
