using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;

namespace nem.Mimir.Benchmarks.Benchmarks;

[MemoryDiagnoser]
public class StreamingHandlerBenchmarks
{
    private readonly MockStreamingHandler _handler = new();

    [Params(1, 10, 50)]
    public int ConcurrentStreams { get; set; }

    [Benchmark(Baseline = true)]
    public async Task<int> StreamResponse()
    {
        var tasks = Enumerable.Range(0, ConcurrentStreams)
            .Select(_ => ConsumeStreamAsync(_handler.StreamAsync("hello", CancellationToken.None)))
            .ToArray();

        var counts = await Task.WhenAll(tasks);
        return counts.Sum();
    }

    private static async Task<int> ConsumeStreamAsync(IAsyncEnumerable<string> stream)
    {
        var count = 0;
        await foreach (var chunk in stream)
        {
            count += chunk.Length;
        }

        return count;
    }

    private sealed class MockStreamingHandler
    {
        public async IAsyncEnumerable<string> StreamAsync(string prompt, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var parts = new[] { "Thinking", "about", prompt, "done" };

            foreach (var part in parts)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();
                yield return part;
            }
        }
    }
}
