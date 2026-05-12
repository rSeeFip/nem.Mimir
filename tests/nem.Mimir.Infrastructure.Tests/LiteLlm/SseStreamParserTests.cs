namespace nem.Mimir.Infrastructure.Tests.LiteLlm;

using System.Text;
using nem.Mimir.Infrastructure.LiteLlm;
using Shouldly;

public sealed class SseStreamParserTests
{
    [Fact]
    public async Task ParseAsync_ValidDataLines_YieldsChunks()
    {
        // Arrange
        var sseData = """
            data: {"id":"chatcmpl-1","model":"phi-4-mini","choices":[{"index":0,"delta":{"content":"Hello"},"finish_reason":null}]}

            data: {"id":"chatcmpl-1","model":"phi-4-mini","choices":[{"index":0,"delta":{"content":" World"},"finish_reason":null}]}

            data: {"id":"chatcmpl-1","model":"phi-4-mini","choices":[{"index":0,"delta":{},"finish_reason":"stop"}]}

            data: [DONE]

            """;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(sseData));

        // Act
        var chunks = new List<Application.Common.Models.LlmStreamChunk>();
        await foreach (var chunk in SseStreamParser.ParseAsync(stream))
        {
            chunks.Add(chunk);
        }

        // Assert
        chunks.Count.ShouldBe(3);
        chunks[0].Content.ShouldBe("Hello");
        chunks[0].Model.ShouldBe("phi-4-mini");
        chunks[0].FinishReason.ShouldBeNull();
        chunks[1].Content.ShouldBe(" World");
        chunks[2].Content.ShouldBe(string.Empty);
        chunks[2].FinishReason.ShouldBe("stop");
    }

    [Fact]
    public async Task ParseAsync_DoneMarker_StopsEnumeration()
    {
        // Arrange
        var sseData = """
            data: {"id":"1","model":"m","choices":[{"index":0,"delta":{"content":"A"},"finish_reason":null}]}

            data: [DONE]

            data: {"id":"2","model":"m","choices":[{"index":0,"delta":{"content":"B"},"finish_reason":null}]}

            """;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(sseData));

        // Act
        var chunks = new List<Application.Common.Models.LlmStreamChunk>();
        await foreach (var chunk in SseStreamParser.ParseAsync(stream))
        {
            chunks.Add(chunk);
        }

        // Assert — should stop at [DONE], never see "B"
        chunks.Count.ShouldBe(1);
        chunks[0].Content.ShouldBe("A");
    }

    [Fact]
    public async Task ParseAsync_EmptyLines_AreSkipped()
    {
        // Arrange
        var sseData = """


            data: {"id":"1","model":"m","choices":[{"index":0,"delta":{"content":"X"},"finish_reason":null}]}



            data: [DONE]

            """;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(sseData));

        // Act
        var chunks = new List<Application.Common.Models.LlmStreamChunk>();
        await foreach (var chunk in SseStreamParser.ParseAsync(stream))
        {
            chunks.Add(chunk);
        }

        // Assert
        chunks.Count.ShouldBe(1);
        chunks[0].Content.ShouldBe("X");
    }

    [Fact]
    public async Task ParseAsync_CommentLines_AreSkipped()
    {
        // Arrange
        var sseData = """
            : this is a comment
            data: {"id":"1","model":"m","choices":[{"index":0,"delta":{"content":"OK"},"finish_reason":null}]}

            data: [DONE]

            """;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(sseData));

        // Act
        var chunks = new List<Application.Common.Models.LlmStreamChunk>();
        await foreach (var chunk in SseStreamParser.ParseAsync(stream))
        {
            chunks.Add(chunk);
        }

        // Assert
        chunks.Count.ShouldBe(1);
        chunks[0].Content.ShouldBe("OK");
    }

    [Fact]
    public async Task ParseAsync_MalformedJson_IsSkipped()
    {
        // Arrange
        var sseData = """
            data: {invalid json here}

            data: {"id":"1","model":"m","choices":[{"index":0,"delta":{"content":"Valid"},"finish_reason":null}]}

            data: [DONE]

            """;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(sseData));

        // Act
        var chunks = new List<Application.Common.Models.LlmStreamChunk>();
        await foreach (var chunk in SseStreamParser.ParseAsync(stream))
        {
            chunks.Add(chunk);
        }

        // Assert — malformed JSON is skipped, valid chunk is yielded
        chunks.Count.ShouldBe(1);
        chunks[0].Content.ShouldBe("Valid");
    }

    [Fact]
    public async Task ParseAsync_NoChoices_ReturnsNull()
    {
        // Arrange
        var sseData = """
            data: {"id":"1","model":"m","choices":[]}

            data: {"id":"2","model":"m"}

            data: [DONE]

            """;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(sseData));

        // Act
        var chunks = new List<Application.Common.Models.LlmStreamChunk>();
        await foreach (var chunk in SseStreamParser.ParseAsync(stream))
        {
            chunks.Add(chunk);
        }

        // Assert — empty/null choices produce no chunks
        chunks.Count.ShouldBe(0);
    }

    [Fact]
    public async Task ParseAsync_NonDataLines_AreIgnored()
    {
        // Arrange — lines not starting with "data: " should be ignored
        var sseData = """
            event: message
            id: 123
            retry: 5000
            data: {"id":"1","model":"m","choices":[{"index":0,"delta":{"content":"Hi"},"finish_reason":null}]}

            data: [DONE]

            """;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(sseData));

        // Act
        var chunks = new List<Application.Common.Models.LlmStreamChunk>();
        await foreach (var chunk in SseStreamParser.ParseAsync(stream))
        {
            chunks.Add(chunk);
        }

        // Assert
        chunks.Count.ShouldBe(1);
        chunks[0].Content.ShouldBe("Hi");
    }

    [Fact]
    public async Task ParseAsync_EmptyStream_YieldsNothing()
    {
        // Arrange
        using var stream = new MemoryStream(Array.Empty<byte>());

        // Act
        var chunks = new List<Application.Common.Models.LlmStreamChunk>();
        await foreach (var chunk in SseStreamParser.ParseAsync(stream))
        {
            chunks.Add(chunk);
        }

        // Assert
        chunks.ShouldBeEmpty();
    }

    [Fact]
    public void ParseChunkJson_ValidJson_ReturnsChunk()
    {
        // Arrange
        var json = """{"id":"1","model":"phi-4-mini","choices":[{"index":0,"delta":{"content":"test"},"finish_reason":null}]}""";

        // Act
        var result = SseStreamParser.ParseChunkJson(json.AsSpan());

        // Assert
        result.ShouldNotBeNull();
        result.Content.ShouldBe("test");
        result.Model.ShouldBe("phi-4-mini");
        result.FinishReason.ShouldBeNull();
    }

    [Fact]
    public void ParseChunkJson_InvalidJson_ReturnsNull()
    {
        // Arrange
        var json = "not valid json";

        // Act
        var result = SseStreamParser.ParseChunkJson(json.AsSpan());

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void ParseChunkJson_EmptyChoices_ReturnsNull()
    {
        // Arrange
        var json = """{"id":"1","model":"m","choices":[]}""";

        // Act
        var result = SseStreamParser.ParseChunkJson(json.AsSpan());

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task ParseAsync_CancellationToken_StopsEnumeration()
    {
        // Arrange
        var sseData = """
            data: {"id":"1","model":"m","choices":[{"index":0,"delta":{"content":"A"},"finish_reason":null}]}

            data: {"id":"2","model":"m","choices":[{"index":0,"delta":{"content":"B"},"finish_reason":null}]}

            data: {"id":"3","model":"m","choices":[{"index":0,"delta":{"content":"C"},"finish_reason":null}]}

            data: [DONE]

            """;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(sseData));
        using var cts = new CancellationTokenSource();

        // Act
        var chunks = new List<Application.Common.Models.LlmStreamChunk>();

        await Should.ThrowAsync<OperationCanceledException>(async () =>
        {
            await foreach (var chunk in SseStreamParser.ParseAsync(stream, cts.Token))
            {
                chunks.Add(chunk);
                if (chunks.Count == 1)
                {
                    cts.Cancel();
                }
            }
        });

        // Assert — should have gotten first chunk before cancellation
        chunks.Count.ShouldBe(1);
        chunks[0].Content.ShouldBe("A");
    }
}
