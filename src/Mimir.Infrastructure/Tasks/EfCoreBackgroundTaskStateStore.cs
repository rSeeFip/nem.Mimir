namespace Mimir.Infrastructure.Tasks;

using Microsoft.EntityFrameworkCore;
using Mimir.Application.Tasks;
using Mimir.Domain.Entities;
using Mimir.Infrastructure.Persistence;
using nem.Contracts.Agents;

internal sealed class EfCoreBackgroundTaskStateStore : IBackgroundTaskStateStore
{
    private const int MaxResultLength = 2048;

    private readonly MimirDbContext _dbContext;

    public EfCoreBackgroundTaskStateStore(MimirDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task RecordSubmittedAsync(AgentTask task, string agentId, CancellationToken cancellationToken = default)
    {
        var entity = BackgroundTask.Create(task.Id, agentId, task.Priority, DateTimeOffset.UtcNow);
        await _dbContext.BackgroundTasks.AddAsync(entity, cancellationToken).ConfigureAwait(false);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task MarkRunningAsync(string taskId, CancellationToken cancellationToken = default)
    {
        var entity = await FindByTaskIdAsync(taskId, cancellationToken).ConfigureAwait(false);
        if (entity is null)
        {
            return;
        }

        entity.MarkRunning(DateTimeOffset.UtcNow);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task MarkCompletedAsync(AgentResult result, CancellationToken cancellationToken = default)
    {
        var entity = await FindByTaskIdAsync(result.TaskId, cancellationToken).ConfigureAwait(false);
        if (entity is null)
        {
            return;
        }

        var summary = CreateResultSummary(result);
        entity.MarkCompleted(DateTimeOffset.UtcNow, summary);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task MarkFailedAsync(string taskId, string error, CancellationToken cancellationToken = default)
    {
        var entity = await FindByTaskIdAsync(taskId, cancellationToken).ConfigureAwait(false);
        if (entity is null)
        {
            return;
        }

        entity.MarkFailed(DateTimeOffset.UtcNow, Truncate(error, 2000));
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task MarkCancelledAsync(string taskId, string? error = null, CancellationToken cancellationToken = default)
    {
        var entity = await FindByTaskIdAsync(taskId, cancellationToken).ConfigureAwait(false);
        if (entity is null)
        {
            return;
        }

        entity.MarkCancelled(DateTimeOffset.UtcNow, Truncate(error, 2000));
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<BackgroundTask?> FindByTaskIdAsync(string taskId, CancellationToken cancellationToken)
    {
        return await _dbContext.BackgroundTasks
            .FirstOrDefaultAsync(x => x.TaskId == taskId, cancellationToken)
            .ConfigureAwait(false);
    }

    private static string CreateResultSummary(AgentResult result)
    {
        var output = result.Output ?? string.Empty;
        var summary = $"Status={result.Status}; Agent={result.AgentId}; Output={output}";
        return Truncate(summary, MaxResultLength) ?? string.Empty;
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength];
    }
}
