namespace nem.Mimir.Infrastructure.Tasks;

using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using nem.Mimir.Application.Tasks;

internal sealed class BackgroundTaskProcessor : BackgroundService
{
    private readonly Channel<BackgroundTaskItem> _taskQueue;
    private readonly BackgroundTaskExecutor _executor;
    private readonly ILogger<BackgroundTaskProcessor> _logger;
    private readonly SemaphoreSlim _concurrencySemaphore;

    public BackgroundTaskProcessor(
        Channel<BackgroundTaskItem> taskQueue,
        BackgroundTaskExecutor executor,
        IOptions<BackgroundTaskOptions> options,
        ILogger<BackgroundTaskProcessor> logger)
    {
        _taskQueue = taskQueue;
        _executor = executor;
        _logger = logger;

        var maxConcurrency = Math.Max(1, options.Value.MaxConcurrency);
        _concurrencySemaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var item in _taskQueue.Reader.ReadAllAsync(stoppingToken))
            {
                await _concurrencySemaphore.WaitAsync(stoppingToken).ConfigureAwait(false);

                _ = ProcessOneAsync(item, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Background task processor is stopping.");
        }
    }

    public override void Dispose()
    {
        _concurrencySemaphore.Dispose();
        base.Dispose();
    }

    private async Task ProcessOneAsync(BackgroundTaskItem item, CancellationToken stoppingToken)
    {
        try
        {
            await _executor.ProcessTaskAsync(item, stoppingToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while processing task {TaskId}.", item.Task.Id);
        }
        finally
        {
            _concurrencySemaphore.Release();
        }
    }
}
