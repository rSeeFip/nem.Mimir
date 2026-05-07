using BenchmarkDotNet.Attributes;
using System.Reflection;

namespace nem.Mimir.Benchmarks.Benchmarks;

[MemoryDiagnoser]
public class ConversationRepositoryBenchmarks
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private BenchmarkConversationRepository _repository = null!;
    private MethodInfo _getByUserIdAsync = null!;

    [GlobalSetup]
    public void Setup()
    {
        _repository = new BenchmarkConversationRepository(SeedConversations());
        _getByUserIdAsync = typeof(BenchmarkConversationRepository).GetMethod(
            nameof(BenchmarkConversationRepository.GetByUserIdAsync),
            BindingFlags.Instance | BindingFlags.Public,
            [typeof(Guid), typeof(int), typeof(int), typeof(CancellationToken)])!;
    }

    [Benchmark(Baseline = true)]
    public async Task GetConversationsPage()
    {
        var task = (Task)_getByUserIdAsync.Invoke(_repository, [UserId, 1, 20, CancellationToken.None])!;
        await task.ConfigureAwait(false);
    }

    private static IReadOnlyList<BenchmarkConversation> SeedConversations()
    {
        var now = DateTimeOffset.UtcNow;
        var conversations = new List<BenchmarkConversation>(250);

        for (var i = 0; i < 250; i++)
        {
            var ownerId = i % 3 == 0 ? UserId : Guid.NewGuid();
            conversations.Add(new BenchmarkConversation(
                Guid.NewGuid(),
                ownerId,
                i % 2 == 0 ? $"Conversation {i} updated" : $"Conversation {i}",
                now.AddMinutes(-i),
                i % 2 == 0 ? now.AddMinutes(-i / 2d) : null));
        }

        return conversations;
    }

    private sealed class BenchmarkConversationRepository(IReadOnlyList<BenchmarkConversation> conversations)
    {
        public Task<IReadOnlyList<BenchmarkConversation>> GetByUserIdAsync(Guid userId, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
        {
            var items = conversations
                .Where(c => c.UserId == userId)
                .OrderByDescending(c => c.UpdatedAt ?? c.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToArray();

            return Task.FromResult<IReadOnlyList<BenchmarkConversation>>(items);
        }
    }

    private sealed record BenchmarkConversation(
        Guid Id,
        Guid UserId,
        string Title,
        DateTimeOffset CreatedAt,
        DateTimeOffset? UpdatedAt = null);
}
