namespace Mimir.Infrastructure.Agents;

using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Mimir.Domain.Agents.Messages;
using Wolverine;

public sealed class AgentCommunicationBus : IAgentCommunicationBus, IAsyncDisposable
{
    private const int MaxPendingMessages = 100;
    private readonly object _pendingLock = new();
    private readonly List<BusEnvelope> _pending = [];
    private readonly SemaphoreSlim _signal = new(0);
    private readonly CancellationTokenSource _shutdown = new();
    private readonly Task _processorTask;
    private readonly ConcurrentDictionary<string, Channel<IAgentMessage>> _agentSubscriptions = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, Channel<IAgentMessage>> _topicSubscriptions = new(StringComparer.OrdinalIgnoreCase);
    private readonly IAgentMessagePersistence _persistence;
    private readonly IMessageBus _messageBus;
    private readonly ILogger<AgentCommunicationBus> _logger;

    public AgentCommunicationBus(
        IAgentMessagePersistence persistence,
        IMessageBus messageBus,
        ILogger<AgentCommunicationBus> logger)
    {
        _persistence = persistence;
        _messageBus = messageBus;
        _logger = logger;
        _processorTask = Task.Run(ProcessAsync);
    }

    public int PendingCount
    {
        get
        {
            lock (_pendingLock)
            {
                return _pending.Count;
            }
        }
    }

    public async Task SendDirectAsync(IAgentMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(message.TargetAgentId))
        {
            throw new InvalidOperationException("Direct messages require TargetAgentId.");
        }

        var enqueued = Enqueue(new BusEnvelope(BusRoute.Direct, message, message.TargetAgentId, null));
        if (!enqueued)
        {
            return;
        }

        await _messageBus.PublishAsync(message).ConfigureAwait(false);
    }

    public async Task BroadcastAsync(IAgentMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        cancellationToken.ThrowIfCancellationRequested();

        var enqueued = Enqueue(new BusEnvelope(BusRoute.Broadcast, message, null, null));
        if (!enqueued)
        {
            return;
        }

        await _messageBus.PublishAsync(message).ConfigureAwait(false);
    }

    public async Task PublishToTopicAsync(string topic, IAgentMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        ArgumentNullException.ThrowIfNull(message);
        cancellationToken.ThrowIfCancellationRequested();

        var enqueued = Enqueue(new BusEnvelope(BusRoute.Topic, message, null, topic));
        if (!enqueued)
        {
            return;
        }

        await _messageBus.PublishAsync(message).ConfigureAwait(false);
    }

    public Task RouteIncomingAsync(IAgentMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        cancellationToken.ThrowIfCancellationRequested();

        var route = ResolveRoute(message);
        _ = Enqueue(route);
        return Task.CompletedTask;
    }

    public ChannelReader<IAgentMessage> SubscribeAgent(string agentId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);
        cancellationToken.ThrowIfCancellationRequested();

        var channel = _agentSubscriptions.GetOrAdd(agentId, static _ => Channel.CreateUnbounded<IAgentMessage>(new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = false,
            AllowSynchronousContinuations = false,
        }));

        return channel.Reader;
    }

    public ChannelReader<IAgentMessage> SubscribeTopic(string topic, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        cancellationToken.ThrowIfCancellationRequested();

        var channel = _topicSubscriptions.GetOrAdd(topic, static _ => Channel.CreateUnbounded<IAgentMessage>(new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = false,
            AllowSynchronousContinuations = false,
        }));

        return channel.Reader;
    }

    public async ValueTask DisposeAsync()
    {
        _shutdown.Cancel();
        _signal.Release();

        try
        {
            await _processorTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }

        _shutdown.Dispose();
        _signal.Dispose();
    }

    private async Task ProcessAsync()
    {
        while (!_shutdown.IsCancellationRequested)
        {
            await _signal.WaitAsync(_shutdown.Token).ConfigureAwait(false);

            BusEnvelope? envelope = null;
            lock (_pendingLock)
            {
                if (_pending.Count > 0)
                {
                    envelope = _pending[0];
                    _pending.RemoveAt(0);
                }
            }

            if (envelope is null)
            {
                continue;
            }

            try
            {
                await _persistence.PersistAsync(envelope.Message, _shutdown.Token).ConfigureAwait(false);
                Dispatch(envelope);
            }
            catch (OperationCanceledException)
            {
                if (_shutdown.IsCancellationRequested)
                {
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process agent message {MessageId}", envelope.Message.MessageId);
            }
        }
    }

    private void Dispatch(BusEnvelope envelope)
    {
        switch (envelope.Route)
        {
            case BusRoute.Direct:
                if (!string.IsNullOrWhiteSpace(envelope.TargetAgentId)
                    && _agentSubscriptions.TryGetValue(envelope.TargetAgentId, out var directChannel))
                {
                    directChannel.Writer.TryWrite(envelope.Message);
                }
                break;

            case BusRoute.Broadcast:
                foreach (var subscriber in _agentSubscriptions.Values)
                {
                    subscriber.Writer.TryWrite(envelope.Message);
                }
                break;

            case BusRoute.Topic:
                if (!string.IsNullOrWhiteSpace(envelope.Topic)
                    && _topicSubscriptions.TryGetValue(envelope.Topic, out var topicChannel))
                {
                    topicChannel.Writer.TryWrite(envelope.Message);
                }
                break;
        }
    }

    private bool Enqueue(BusEnvelope envelope)
    {
        lock (_pendingLock)
        {
            if (_pending.Count >= MaxPendingMessages)
            {
                var dropped = DropOldestLowPriorityLocked();
                if (!dropped && envelope.Message.Priority <= AgentMessagePriority.Normal)
                {
                    return false;
                }
            }

            _pending.Add(envelope);
        }

        _signal.Release();
        return true;
    }

    private bool DropOldestLowPriorityLocked()
    {
        for (var i = 0; i < _pending.Count; i++)
        {
            if (_pending[i].Message.Priority <= AgentMessagePriority.Normal)
            {
                _pending.RemoveAt(i);
                return true;
            }
        }

        return false;
    }

    private static BusEnvelope ResolveRoute(IAgentMessage message)
    {
        if (!string.IsNullOrWhiteSpace(message.TargetAgentId))
        {
            return new BusEnvelope(BusRoute.Direct, message, message.TargetAgentId, null);
        }

        if (!string.IsNullOrWhiteSpace(message.Topic))
        {
            return new BusEnvelope(BusRoute.Topic, message, null, message.Topic);
        }

        return new BusEnvelope(BusRoute.Broadcast, message, null, null);
    }

    private sealed record BusEnvelope(BusRoute Route, IAgentMessage Message, string? TargetAgentId, string? Topic);

    private enum BusRoute
    {
        Direct,
        Broadcast,
        Topic,
    }
}
