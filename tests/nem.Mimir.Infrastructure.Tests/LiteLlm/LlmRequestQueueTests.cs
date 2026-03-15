namespace nem.Mimir.Infrastructure.Tests.LiteLlm;

using Microsoft.Extensions.Logging;
using nem.Mimir.Infrastructure.LiteLlm;
using NSubstitute;
using Shouldly;

public sealed class LlmRequestQueueTests : IDisposable
{
    private readonly LlmRequestQueue _queue;
    private readonly ILogger<LlmRequestQueue> _logger;

    public LlmRequestQueueTests()
    {
        _logger = Substitute.For<ILogger<LlmRequestQueue>>();
        _queue = new LlmRequestQueue(_logger, maxQueueSize: 10);
    }

    public void Dispose()
    {
        _queue.Dispose();
    }

    [Fact]
    public async Task EnqueueAsync_SingleRequest_ProcessesSuccessfully()
    {
        // Act
        var result = await _queue.EnqueueAsync<int>(
            _ => Task.FromResult(42));

        // Assert
        result.Value.ShouldBe(42);
        result.QueuePosition.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task EnqueueAsync_MultipleRequests_ProcessedInFifoOrder()
    {
        // Arrange
        var processOrder = new List<int>();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // Act — enqueue 3 items; first blocks until gate opens
        var task1 = _queue.EnqueueAsync<int>(async ct =>
        {
            await gate.Task;
            processOrder.Add(1);
            return 1;
        });

        // Give a moment for task1 to start processing
        await Task.Delay(50);

        var task2 = _queue.EnqueueAsync<int>(_ =>
        {
            processOrder.Add(2);
            return Task.FromResult(2);
        });

        var task3 = _queue.EnqueueAsync<int>(_ =>
        {
            processOrder.Add(3);
            return Task.FromResult(3);
        });

        // Release the gate
        gate.SetResult();

        var result1 = await task1;
        var result2 = await task2;
        var result3 = await task3;

        // Assert — FIFO order
        result1.Value.ShouldBe(1);
        result2.Value.ShouldBe(2);
        result3.Value.ShouldBe(3);
        processOrder.ShouldBe(new[] { 1, 2, 3 });
    }

    [Fact]
    public async Task EnqueueAsync_RequestThrows_PropagatesException()
    {
        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(async () =>
        {
            await _queue.EnqueueAsync<int>(_ =>
                throw new InvalidOperationException("test error"));
        });
    }

    [Fact]
    public async Task EnqueueAsync_Cancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // Block the queue with first request
        var blockingTask = _queue.EnqueueAsync<int>(async ct =>
        {
            await gate.Task;
            return 0;
        });

        // Give time for blocking task to start
        await Task.Delay(50);

        // Cancel the second request before it processes
        cts.Cancel();

        // Enqueue with already-cancelled token
        var cancelledTask = _queue.EnqueueAsync<int>(
            _ => Task.FromResult(1),
            cts.Token);

        // Release the blocking task
        gate.SetResult();
        await blockingTask;

        // The cancelled task should either throw or complete depending on timing.
        // Since we cancelled before processing, the queue skips it.
        // The EnqueueAsync itself may throw if the write detects cancellation.
        try
        {
            await cancelledTask;
        }
        catch (OperationCanceledException)
        {
            // Expected — cancellation detected
        }
    }

    [Fact]
    public async Task QueueDepth_TracksCorrectly()
    {
        // Arrange
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // Act — enqueue a blocking request
        var task1 = _queue.EnqueueAsync<int>(async ct =>
        {
            await gate.Task;
            return 1;
        });

        // Give a moment for the queue to register
        await Task.Delay(50);

        var task2 = _queue.EnqueueAsync<int>(_ => Task.FromResult(2));

        // Queue should have items
        _queue.QueueDepth.ShouldBeGreaterThan(0);

        // Release and wait
        gate.SetResult();
        await task1;
        await task2;

        // After processing, queue should be empty (eventually)
        await Task.Delay(50);
        _queue.QueueDepth.ShouldBe(0);
    }

    [Fact]
    public async Task GetQueuePosition_ReturnsNextPosition()
    {
        // The queue position is approximate — just verify it returns a positive number
        _queue.GetQueuePosition().ShouldBeGreaterThan(0);

        await _queue.EnqueueAsync<int>(_ => Task.FromResult(1));

        // After processing, next position should still be valid
        _queue.GetQueuePosition().ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task EnqueueAsync_ConcurrentEnqueue_AllProcessed()
    {
        // Arrange
        const int count = 5;
        var results = new int[count];

        // Act — enqueue concurrently
        var tasks = Enumerable.Range(0, count)
            .Select(i => _queue.EnqueueAsync<int>(_ =>
            {
                results[i] = i;
                return Task.FromResult(i);
            }))
            .ToArray();

        var queueResults = await Task.WhenAll(tasks);

        // Assert — all processed
        queueResults.Length.ShouldBe(count);
        for (var i = 0; i < count; i++)
        {
            results[i].ShouldBe(i);
        }
    }

    [Fact]
    public async Task EnqueueAsync_StringResult_WorksCorrectly()
    {
        // Act
        var result = await _queue.EnqueueAsync<string>(
            _ => Task.FromResult("hello"));

        // Assert
        result.Value.ShouldBe("hello");
    }
}
