namespace Mimir.Application.Tasks;

using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using nem.Contracts.Agents;

public sealed class BackgroundTaskExecutor : IBackgroundTaskExecutor
{
    private readonly IMemoryCache _memoryCache;
    private readonly IBackgroundTaskRuntime _runtime;
    private readonly Channel<BackgroundTaskItem> _taskQueue;
    private readonly ILogger<BackgroundTaskExecutor> _logger;
    private readonly BackgroundTaskOptions _options;
    private readonly ConcurrentDictionary<string, BackgroundTaskItem> _activeTasks = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, AgentResult> _statusByTaskId = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, IProgress<TaskProgress>?> _progressByTaskId = new(StringComparer.OrdinalIgnoreCase);

    public BackgroundTaskExecutor(
        IMemoryCache memoryCache,
        IBackgroundTaskRuntime runtime,
        Channel<BackgroundTaskItem> taskQueue,
        IOptions<BackgroundTaskOptions> options,
        ILogger<BackgroundTaskExecutor> logger)
    {
        _memoryCache = memoryCache;
        _runtime = runtime;
        _taskQueue = taskQueue;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<string> SubmitAsync(AgentTask task, CancellationToken cancellationToken = default)
    {
        return await SubmitAsync(task, progress: null, cancellationToken).ConfigureAwait(false);
    }

    public async Task<string> SubmitAsync(
        AgentTask task,
        IProgress<TaskProgress>? progress,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var taskId = string.IsNullOrWhiteSpace(task.Id)
            ? Guid.NewGuid().ToString("N")
            : task.Id;

        var normalizedTask = task with { Id = taskId };
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var effectiveProgress = progress ?? ResolveProgress(normalizedTask.Context);
        var item = new BackgroundTaskItem(normalizedTask, linkedCts, effectiveProgress);

        _activeTasks[taskId] = item;
        _progressByTaskId[taskId] = effectiveProgress;
        _statusByTaskId[taskId] = new AgentResult(taskId, "background-executor", BackgroundTaskStatus.Queued);

        await _runtime.RecordSubmittedAsync(normalizedTask, ResolveAgentId(normalizedTask), cancellationToken).ConfigureAwait(false);
        ReportProgress(taskId, 0, "Queued");

        await _taskQueue.Writer.WriteAsync(item, cancellationToken).ConfigureAwait(false);
        return taskId;
    }

    public Task<AgentResult?> GetResultAsync(string taskId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(taskId))
        {
            return Task.FromResult<AgentResult?>(null);
        }

        if (_memoryCache.TryGetValue<AgentResult>(CacheKey(taskId), out var cached))
        {
            return Task.FromResult<AgentResult?>(cached);
        }

        _statusByTaskId.TryGetValue(taskId, out var result);
        return Task.FromResult<AgentResult?>(result);
    }

    public Task<IReadOnlyList<AgentTask>> ListActiveAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<AgentTask> active = _activeTasks.Values.Select(x => x.Task).ToList();
        return Task.FromResult(active);
    }

    public async Task CancelAsync(string taskId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(taskId))
        {
            return;
        }

        if (_activeTasks.TryGetValue(taskId, out var item))
        {
            item.CancellationTokenSource.Cancel();
        }

        await _runtime.CancelExecutionAsync(taskId, cancellationToken).ConfigureAwait(false);
        var cancelledResult = new AgentResult(taskId, "background-executor", BackgroundTaskStatus.Cancelled);
        _statusByTaskId[taskId] = cancelledResult;
        await _runtime.MarkCancelledAsync(taskId, cancellationToken: cancellationToken).ConfigureAwait(false);
        ReportProgress(taskId, 100, "Cancelled");
    }

    public async Task ProcessTaskAsync(BackgroundTaskItem item, CancellationToken stoppingToken)
    {
        var taskId = item.Task.Id;
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, item.CancellationTokenSource.Token);

        var timeoutSeconds = item.Task.TimeoutSeconds > 0
            ? item.Task.TimeoutSeconds
            : _options.DefaultTimeoutSeconds;

        linkedCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        _statusByTaskId[taskId] = new AgentResult(taskId, "background-executor", BackgroundTaskStatus.Running);
        await _runtime.MarkRunningAsync(taskId, stoppingToken).ConfigureAwait(false);
        ReportProgress(taskId, 10, "Running");

        try
        {
            var result = await _runtime.DispatchAsync(item.Task, linkedCts.Token).ConfigureAwait(false);
            _statusByTaskId[taskId] = result;

            if (result.Status.Equals(BackgroundTaskStatus.Cancelled, StringComparison.OrdinalIgnoreCase))
            {
                await _runtime.MarkCancelledAsync(taskId, cancellationToken: stoppingToken).ConfigureAwait(false);
            }
            else if (result.Status.Equals(BackgroundTaskStatus.Failed, StringComparison.OrdinalIgnoreCase)
                     || result.Status.Equals("TimedOut", StringComparison.OrdinalIgnoreCase))
            {
                await _runtime.MarkFailedAsync(taskId, result.Output ?? "Task failed.", stoppingToken).ConfigureAwait(false);
            }
            else
            {
                await _runtime.MarkCompletedAsync(result, stoppingToken).ConfigureAwait(false);
            }

            _memoryCache.Set(
                CacheKey(taskId),
                result,
                TimeSpan.FromMinutes(Math.Max(1, _options.ResultCacheMinutes)));

            ReportProgress(taskId, 100, result.Status);
        }
        catch (OperationCanceledException) when (linkedCts.IsCancellationRequested)
        {
            var timedOutOrCancelled = item.CancellationTokenSource.IsCancellationRequested
                ? new AgentResult(taskId, "background-executor", BackgroundTaskStatus.Cancelled)
                : new AgentResult(taskId, "background-executor", "TimedOut", "Task execution timed out.");

            _statusByTaskId[taskId] = timedOutOrCancelled;

            if (item.CancellationTokenSource.IsCancellationRequested)
            {
                await _runtime.MarkCancelledAsync(taskId, cancellationToken: CancellationToken.None).ConfigureAwait(false);
                ReportProgress(taskId, 100, "Cancelled");
            }
            else
            {
                await _runtime.MarkFailedAsync(taskId, "Task execution timed out.", CancellationToken.None).ConfigureAwait(false);
                ReportProgress(taskId, 100, "TimedOut");
            }

            _memoryCache.Set(
                CacheKey(taskId),
                timedOutOrCancelled,
                TimeSpan.FromMinutes(Math.Max(1, _options.ResultCacheMinutes)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Background task {TaskId} failed.", taskId);

            var failed = new AgentResult(taskId, "background-executor", BackgroundTaskStatus.Failed, ex.Message);
            _statusByTaskId[taskId] = failed;

            await _runtime.MarkFailedAsync(taskId, ex.Message, CancellationToken.None).ConfigureAwait(false);
            _memoryCache.Set(
                CacheKey(taskId),
                failed,
                TimeSpan.FromMinutes(Math.Max(1, _options.ResultCacheMinutes)));
            ReportProgress(taskId, 100, "Failed");
        }
        finally
        {
            _activeTasks.TryRemove(taskId, out _);
            if (_progressByTaskId.TryRemove(taskId, out _))
            {
                // no-op: removed to avoid long-lived references
            }

            item.CancellationTokenSource.Dispose();
        }
    }

    private static string ResolveAgentId(AgentTask task)
    {
        if (task.Context is not null
            && task.Context.TryGetValue("agentId", out var configuredAgentId)
            && !string.IsNullOrWhiteSpace(configuredAgentId))
        {
            return configuredAgentId;
        }

        return "orchestrator";
    }

    private IProgress<TaskProgress>? ResolveProgress(IReadOnlyDictionary<string, string>? context)
    {
        if (context is null)
        {
            return null;
        }

        if (!context.TryGetValue("progressKey", out var progressKey)
            || string.IsNullOrWhiteSpace(progressKey))
        {
            return null;
        }

        return new Progress<TaskProgress>(_ =>
            _logger.LogDebug("Task progress event emitted for key {ProgressKey}.", progressKey));
    }

    private void ReportProgress(string taskId, int percent, string message)
    {
        if (_progressByTaskId.TryGetValue(taskId, out var progress) && progress is not null)
        {
            progress.Report(new TaskProgress(taskId, percent, message, DateTimeOffset.UtcNow));
        }
    }

    private static string CacheKey(string taskId) => $"background-task-result:{taskId}";
}
