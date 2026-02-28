namespace Mimir.Infrastructure.LiteLlm;

using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Mimir.Application.Common.Models;

/// <summary>
/// Parses Server-Sent Events (SSE) streams from the LiteLLM proxy
/// and yields <see cref="LlmStreamChunk"/> records for each content token.
/// </summary>
internal static class SseStreamParser
{

    /// <summary>
    /// Reads an SSE stream and yields parsed <see cref="LlmStreamChunk"/> records.
    /// </summary>
    /// <param name="stream">The HTTP response stream containing SSE data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async IAsyncEnumerable<LlmStreamChunk> ParseAsync(
        Stream stream,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);

        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false)) is not null)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Skip empty lines (SSE event separator) and comments (lines starting with ':')
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith(':'))
            {
                continue;
            }

            // Only process data lines
            if (!line.StartsWith("data: ", StringComparison.Ordinal))
            {
                continue;
            }

            var data = line.AsSpan(6); // Skip "data: " prefix

            // Check for stream termination marker
            if (data.SequenceEqual("[DONE]"))
            {
                yield break;
            }

            // Parse the JSON chunk
            var chunk = ParseChunkJson(data);

            if (chunk is not null)
            {
                yield return chunk;
            }
        }
    }

    /// <summary>
    /// Parses a single SSE data line (JSON) into an <see cref="LlmStreamChunk"/>.
    /// Returns null if the JSON is malformed or contains no content.
    /// </summary>
    internal static LlmStreamChunk? ParseChunkJson(ReadOnlySpan<char> json)
    {
        try
        {
            var chunk = JsonSerializer.Deserialize<ChatCompletionChunk>(json);

            if (chunk?.Choices is not { Count: > 0 })
            {
                return null;
            }

            var choice = chunk.Choices[0];
            var content = choice.Delta?.Content ?? string.Empty;
            var finishReason = choice.FinishReason;
            var model = chunk.Model;

            return new LlmStreamChunk(content, model, finishReason);
        }
        catch (JsonException)
        {
            // Skip malformed JSON lines gracefully
            return null;
        }
    }
}
