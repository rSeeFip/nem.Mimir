using System.Threading.Channels;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using nem.Mimir.Application.Tasks;
using NSubstitute;
using Shouldly;
using nem.Contracts.Agents;

namespace nem.Mimir.Application.Tests.Tasks;

public sealed class BackgroundTaskExecutorTests
{
    private readonly IBackgroundTaskRuntime _runtime = Substitute.For<IBackgroundTaskRuntime>();
    private readonly ILogger<BackgroundTaskExecutor> _logger = Substitute.For<ILogger<BackgroundTaskExecutor>>();

    [Fact]
    public async Task SubmitAsync_ShouldQueueAndListTaskAsActive()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var channel = Channel.CreateUnbounded<BackgroundTaskItem>();
        var sut = CreateSut(cache, channel);

        var task = new AgentTask("task-1", AgentTaskType.Execute, "run tests");

        var taskId = await sut.SubmitAsync(task);
        var active = await sut.ListActiveAsync();

        taskId.ShouldBe("task-1");
        active.Count.ShouldBe(1);
        active[0].Id.ShouldBe("task-1");

        await _runtime.Received(1)
            .RecordSubmittedAsync(Arg.Is<AgentTask>(x => x.Id == "task-1"), "orchestrator", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessTaskAsync_ShouldCompleteAndCacheResult()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var channel = Channel.CreateUnbounded<BackgroundTaskItem>();
        var sut = CreateSut(cache, channel);

        var task = new AgentTask("task-2", AgentTaskType.Explore, "explore code");
        var expected = new AgentResult(task.Id, "agent-1", "Completed", "done");

        _runtime.DispatchAsync(Arg.Any<AgentTask>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        var progressEvents = new List<TaskProgress>();
        var progress = new Progress<TaskProgress>(x => progressEvents.Add(x));

        await sut.SubmitAsync(task, progress);
        var queuedItem = await channel.Reader.ReadAsync();
        await sut.ProcessTaskAsync(queuedItem, CancellationToken.None);

        var result = await sut.GetResultAsync(task.Id);
        var active = await sut.ListActiveAsync();

        result.ShouldNotBeNull();
        result.Status.ShouldBe("Completed");
        result.Output.ShouldBe("done");
        active.Count.ShouldBe(0);
        progressEvents.Count.ShouldBeGreaterThanOrEqualTo(2);
        progressEvents.ShouldContain(x => x.StatusMessage == "Running");
        progressEvents.ShouldContain(x => x.StatusMessage == "Completed");

        await _runtime.Received(1).MarkRunningAsync(task.Id, Arg.Any<CancellationToken>());
        await _runtime.Received(1).MarkCompletedAsync(expected, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CancelAsync_ShouldCancelActiveTask_AndRecordCancelledStatus()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var channel = Channel.CreateUnbounded<BackgroundTaskItem>();
        var sut = CreateSut(cache, channel);

        var task = new AgentTask("task-3", AgentTaskType.Analyze, "analyze issue");

        await sut.SubmitAsync(task);
        await sut.CancelAsync(task.Id);

        var result = await sut.GetResultAsync(task.Id);

        result.ShouldNotBeNull();
        result.Status.ShouldBe("Cancelled");

        await _runtime.Received(1).CancelExecutionAsync(task.Id, Arg.Any<CancellationToken>());
        await _runtime.Received(1).MarkCancelledAsync(task.Id, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessTaskAsync_ShouldMarkTimedOut_WhenRuntimeCancelsByTimeout()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var channel = Channel.CreateUnbounded<BackgroundTaskItem>();
        var sut = CreateSut(cache, channel);

        var task = new AgentTask("task-4", AgentTaskType.Execute, "long running", TimeoutSeconds: 1);

        _runtime.DispatchAsync(Arg.Any<AgentTask>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                var ct = call.Arg<CancellationToken>();
                await Task.Delay(TimeSpan.FromSeconds(5), ct);
                return new AgentResult(task.Id, "agent", "Completed");
            });

        await sut.SubmitAsync(task);
        var queuedItem = await channel.Reader.ReadAsync();
        await sut.ProcessTaskAsync(queuedItem, CancellationToken.None);

        var result = await sut.GetResultAsync(task.Id);

        result.ShouldNotBeNull();
        result.Status.ShouldBe("TimedOut");

        await _runtime.Received(1)
            .MarkFailedAsync(task.Id, "Task execution timed out.", Arg.Any<CancellationToken>());
    }

    private BackgroundTaskExecutor CreateSut(IMemoryCache cache, Channel<BackgroundTaskItem> channel)
    {
        var options = Options.Create(new BackgroundTaskOptions
        {
            MaxConcurrency = 2,
            DefaultTimeoutSeconds = 300,
            ResultCacheMinutes = 60,
        });

        return new BackgroundTaskExecutor(cache, _runtime, channel, options, _logger);
    }
}
