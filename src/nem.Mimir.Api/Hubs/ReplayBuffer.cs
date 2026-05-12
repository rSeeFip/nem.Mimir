using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using nem.Mimir.Domain.Enums;

namespace nem.Mimir.Api.Hubs;

public interface IReplayBuffer
{
    Task RecordMessageAsync(
        Guid conversationId,
        Guid userId,
        ReplayMessage message,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ReplayMessage>> GetReplayAsync(
        Guid conversationId,
        Guid userId,
        CancellationToken cancellationToken = default);
}

public sealed record ReplayMessage(
    MessageRole Role,
    string Content,
    DateTimeOffset CreatedAt);

public sealed class ReplayBuffer : IReplayBuffer
{
    private readonly TimeProvider _timeProvider;
    private readonly ReplayBufferOptions _options;

    private readonly ConcurrentDictionary<ReplayBufferKey, ReplayBucket> _buffers = new();

    public ReplayBuffer(TimeProvider timeProvider, IOptions<ReplayBufferOptions> options)
    {
        _timeProvider = timeProvider;
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value ?? new ReplayBufferOptions();
    }

    public Task RecordMessageAsync(
        Guid conversationId,
        Guid userId,
        ReplayMessage message,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var key = new ReplayBufferKey(conversationId, userId);
        var bucket = _buffers.GetOrAdd(key, static _ => new ReplayBucket());

        lock (bucket.Sync)
        {
            bucket.Messages.Add(message);
            PruneUnsafe(bucket.Messages);

            if (bucket.Messages.Count == 0)
            {
                _buffers.TryRemove(key, out _);
            }
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ReplayMessage>> GetReplayAsync(
        Guid conversationId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var key = new ReplayBufferKey(conversationId, userId);
        if (!_buffers.TryGetValue(key, out var bucket))
        {
            return Task.FromResult<IReadOnlyList<ReplayMessage>>([]);
        }

        lock (bucket.Sync)
        {
            PruneUnsafe(bucket.Messages);

            if (bucket.Messages.Count == 0)
            {
                _buffers.TryRemove(key, out _);
                return Task.FromResult<IReadOnlyList<ReplayMessage>>([]);
            }

            return Task.FromResult<IReadOnlyList<ReplayMessage>>(bucket.Messages.ToArray());
        }
    }

    private void PruneUnsafe(List<ReplayMessage> messages)
    {
        var cutoff = _timeProvider.GetUtcNow() - TimeSpan.FromMinutes(Math.Max(0, _options.WindowMinutes));
        messages.RemoveAll(message => message.CreatedAt < cutoff);

        if (messages.Count <= Math.Max(0, _options.MaxMessages))
        {
            return;
        }

        messages.RemoveRange(0, messages.Count - Math.Max(0, _options.MaxMessages));
    }

    private readonly record struct ReplayBufferKey(Guid ConversationId, Guid UserId);

    private sealed class ReplayBucket
    {
        public object Sync { get; } = new();

        public List<ReplayMessage> Messages { get; } = [];
    }
}
