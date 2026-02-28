namespace Mimir.Infrastructure.LiteLlm;

using System.Threading.Channels;
using Microsoft.Extensions.Logging;

/// <summary>
/// A bounded FIFO request queue for serializing LLM requests.
/// LM Studio processes one request at a time, so this queue ensures
/// orderly processing with user-visible queue position tracking.
/// </summary>
public sealed class LlmRequestQueue : IDisposable
{
    private readonly Channel<QueuedRequest> _channel;
    private readonly ILogger<LlmRequestQueue> _logger;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _processingTask;
    private int _queuedCount;

    public LlmRequestQueue(ILogger<LlmRequestQueue> logger, int maxQueueSize = 100)
    {
        _logger = logger;
        _channel = Channel.CreateBounded<QueuedRequest>(new BoundedChannelOptions(maxQueueSize)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false,
        });

        _processingTask = ProcessQueueAsync(_cts.Token);
    }

    /// <summary>
    /// Gets the current number of items waiting in the queue.
    /// </summary>
    public int QueueDepth => _queuedCount;

    /// <summary>
    /// Enqueues a request and returns a task that completes when the request is processed.
    /// </summary>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    /// <param name="executeAsync">The async function to execute when the request reaches the front of the queue.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task containing the result and the queue position at time of enqueue.</returns>
    public async Task<QueueResult<TResult>> EnqueueAsync<TResult>(
        Func<CancellationToken, Task<TResult>> executeAsync,
        CancellationToken cancellationToken = default)
    {
        var tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var position = Interlocked.Increment(ref _queuedCount);

        var request = new QueuedRequest(
            async ct =>
            {
                try
                {
                    var result = await executeAsync(ct).ConfigureAwait(false);
                    tcs.TrySetResult(result);
                }
                catch (OperationCanceledException)
                {
                    tcs.TrySetCanceled(ct);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            },
            cancellationToken);

        await _channel.Writer.WriteAsync(request, cancellationToken).ConfigureAwait(false);

        _logger.LogDebug("Request enqueued at position {Position}", position);

        var result = await tcs.Task.ConfigureAwait(false);

        return new QueueResult<TResult>((TResult)result!, position);
    }

    /// <summary>
    /// Gets the approximate queue position for a new request (1-based).
    /// </summary>
    public int GetQueuePosition() => _queuedCount + 1;

    private async Task ProcessQueueAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var request in _channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                if (request.CallerCancellationToken.IsCancellationRequested)
                {
                    Interlocked.Decrement(ref _queuedCount);
                    continue;
                }

                try
                {
                    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                        cancellationToken, request.CallerCancellationToken);
                    await request.ExecuteAsync(linkedCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing queued LLM request");
                }
                finally
                {
                    Interlocked.Decrement(ref _queuedCount);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
    }

    public void Dispose()
    {
        _channel.Writer.TryComplete();
        _cts.Cancel();
        _cts.Dispose();

        // Don't await _processingTask here — fire-and-forget is fine for disposal
    }

    internal sealed record QueuedRequest(
        Func<CancellationToken, Task> ExecuteAsync,
        CancellationToken CallerCancellationToken);
}

/// <summary>
/// Result from a queued request, including the queue position at time of enqueue.
/// </summary>
public sealed record QueueResult<TResult>(TResult Value, int QueuePosition);
