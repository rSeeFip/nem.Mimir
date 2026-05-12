namespace nem.Mimir.Infrastructure.Tasks;

using Microsoft.Extensions.DependencyInjection;
using nem.Mimir.Application.Tasks;
using nem.Contracts.Agents;

internal sealed class BackgroundTaskRuntime : IBackgroundTaskRuntime
{
    private readonly IServiceScopeFactory _scopeFactory;

    public BackgroundTaskRuntime(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task RecordSubmittedAsync(AgentTask task, string agentId, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IBackgroundTaskStateStore>();
        await store.RecordSubmittedAsync(task, agentId, cancellationToken).ConfigureAwait(false);
    }

    public async Task MarkRunningAsync(string taskId, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IBackgroundTaskStateStore>();
        await store.MarkRunningAsync(taskId, cancellationToken).ConfigureAwait(false);
    }

    public async Task MarkCompletedAsync(AgentResult result, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IBackgroundTaskStateStore>();
        await store.MarkCompletedAsync(result, cancellationToken).ConfigureAwait(false);
    }

    public async Task MarkFailedAsync(string taskId, string error, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IBackgroundTaskStateStore>();
        await store.MarkFailedAsync(taskId, error, cancellationToken).ConfigureAwait(false);
    }

    public async Task MarkCancelledAsync(string taskId, string? error = null, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IBackgroundTaskStateStore>();
        await store.MarkCancelledAsync(taskId, error, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AgentResult> DispatchAsync(AgentTask task, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var orchestrator = scope.ServiceProvider.GetRequiredService<IAgentOrchestrator>();
        return await orchestrator.DispatchAsync(task, cancellationToken).ConfigureAwait(false);
    }

    public async Task CancelExecutionAsync(string taskId, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var orchestrator = scope.ServiceProvider.GetRequiredService<IAgentOrchestrator>();
        await orchestrator.CancelTaskAsync(taskId, cancellationToken).ConfigureAwait(false);
    }
}
