namespace nem.Mimir.Infrastructure.Tests.Agents;

using nem.Mimir.Domain.Agents.Messages;
using nem.Mimir.Infrastructure.Agents;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Wolverine;

public sealed class AgentCommunicationBusTests : IAsyncDisposable
{
    private readonly IAgentMessagePersistence _persistence = Substitute.For<IAgentMessagePersistence>();
    private readonly IMessageBus _messageBus = Substitute.For<IMessageBus>();
    private readonly ILogger<AgentCommunicationBus> _logger = Substitute.For<ILogger<AgentCommunicationBus>>();
    private readonly AgentCommunicationBus _sut;

    public AgentCommunicationBusTests()
    {
        _sut = new AgentCommunicationBus(_persistence, _messageBus, _logger);
    }

    public async ValueTask DisposeAsync()
    {
        await _sut.DisposeAsync();
    }

    [Fact]
    public async Task SendDirectAsync_DeliversMessageToTargetSubscriber()
    {
        var reader = _sut.SubscribeAgent("agent-beta");
        var message = new AgentTaskRequest(
            Guid.NewGuid(),
            "agent-alpha",
            "agent-beta",
            DateTimeOffset.UtcNow,
            AgentMessagePriority.Normal,
            "task-1",
            "analyze repository");

        await _sut.SendDirectAsync(message, CancellationToken.None);
        var delivered = await reader.ReadAsync();

        delivered.ShouldBe(message);
        await _persistence.Received(1).PersistAsync(message, Arg.Any<CancellationToken>());
        await _messageBus.Received(1).PublishAsync(message);
    }

    [Fact]
    public async Task BroadcastAsync_DeliversToAllSubscribers()
    {
        var readerA = _sut.SubscribeAgent("agent-a");
        var readerB = _sut.SubscribeAgent("agent-b");
        var message = new AgentStatusUpdate(
            Guid.NewGuid(),
            "orchestrator",
            null,
            DateTimeOffset.UtcNow,
            AgentMessagePriority.High,
            "Busy",
            "Working");

        await _sut.BroadcastAsync(message, CancellationToken.None);

        var deliveredA = await readerA.ReadAsync();
        var deliveredB = await readerB.ReadAsync();

        deliveredA.ShouldBe(message);
        deliveredB.ShouldBe(message);
    }

    [Fact]
    public async Task PublishToTopicAsync_DeliversToTopicSubscribers()
    {
        var reader = _sut.SubscribeTopic("knowledge");
        var message = new AgentKnowledgeShare(
            Guid.NewGuid(),
            "agent-a",
            null,
            DateTimeOffset.UtcNow,
            AgentMessagePriority.Normal,
            "fact",
            "Use channels for in-memory transport",
            Topic: "knowledge");

        await _sut.PublishToTopicAsync("knowledge", message, CancellationToken.None);
        var delivered = await reader.ReadAsync();

        delivered.ShouldBe(message);
    }

    [Fact]
    public async Task RouteIncomingAsync_UsesDirectRoutingForTargetedMessage()
    {
        var reader = _sut.SubscribeAgent("agent-target");
        var message = new AgentEscalationRequest(
            Guid.NewGuid(),
            "agent-a",
            "agent-target",
            DateTimeOffset.UtcNow,
            AgentMessagePriority.Critical,
            "task-2",
            "requires privileged capability",
            "privileged-capability");

        await _sut.RouteIncomingAsync(message, CancellationToken.None);
        var delivered = await reader.ReadAsync();

        delivered.ShouldBe(message);
    }

    [Fact]
    public async Task Backpressure_DropsLowPriorityMessagesWhenQueueExceedsCapacity()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _persistence
            .PersistAsync(Arg.Any<IAgentMessage>(), Arg.Any<CancellationToken>())
            .Returns(_ => gate.Task);

        var publishTasks = new List<Task>();
        for (var i = 0; i < 150; i++)
        {
            var message = new AgentStatusUpdate(
                Guid.NewGuid(),
                "agent-a",
                null,
                DateTimeOffset.UtcNow,
                AgentMessagePriority.Low,
                "Queueing",
                i.ToString());
            publishTasks.Add(_sut.BroadcastAsync(message, CancellationToken.None));
        }

        await Task.WhenAll(publishTasks);

        _sut.PendingCount.ShouldBeLessThanOrEqualTo(100);

        gate.SetResult();
    }
}
