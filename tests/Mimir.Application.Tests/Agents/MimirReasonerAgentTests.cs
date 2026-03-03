using Microsoft.Extensions.Logging;
using Mimir.Application.Agents;
using nem.Cognitive.Abstractions.Interfaces;
using nem.Cognitive.Abstractions.Models;
using nem.Cognitive.Agents.Models;
using NSubstitute;
using Shouldly;

namespace Mimir.Application.Tests.Agents;

public sealed class MimirReasonerAgentTests
{
    private readonly ICognitiveClient _cognitiveClient;
    private readonly ConversationMemoryService _memoryService;
    private readonly QueryRouter _queryRouter;
    private readonly MimirReasonerAgent _agent;

    public MimirReasonerAgentTests()
    {
        _cognitiveClient = Substitute.For<ICognitiveClient>();
        var memoryLogger = Substitute.For<ILogger<ConversationMemoryService>>();
        _memoryService = new ConversationMemoryService(memoryLogger);
        _queryRouter = new QueryRouter();
        var agentLogger = Substitute.For<ILogger<MimirReasonerAgent>>();

        _agent = new MimirReasonerAgent(
            _cognitiveClient, _memoryService, _queryRouter, agentLogger);
    }

    [Fact]
    public void Identity_ShouldHaveCorrectServiceAndAgentName()
    {
        _agent.Identity.ServiceName.ShouldBe("Mimir");
        _agent.Identity.AgentName.ShouldBe("Reasoner");
        _agent.Identity.SystemPrompt.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void Autonomy_ShouldBeL0Execute()
    {
        _agent.Autonomy.ShouldBe(AutonomyLevel.L0_Execute);
    }

    [Fact]
    public void AvailableTools_ShouldHaveThreeTools()
    {
        _agent.AvailableTools.Count.ShouldBe(3);
        _agent.AvailableTools.ShouldContain(t => t.Name == "AnalyzeIntent");
        _agent.AvailableTools.ShouldContain(t => t.Name == "RouteToService");
        _agent.AvailableTools.ShouldContain(t => t.Name == "ReasonAboutQuery");
    }

    [Fact]
    public async Task InvokeAsync_ValidInput_ReturnsAgentResponse()
    {
        var chatResponse = new ChatResponse("Test response", "qwen2.5:72b", 10, 20, 30, "stop");
        _cognitiveClient.ChatAsync(
                Arg.Any<string>(),
                Arg.Any<IReadOnlyList<ChatMessage>>(),
                Arg.Any<CognitiveOptions>(),
                Arg.Any<CancellationToken>())
            .Returns(chatResponse);

        var result = await _agent.InvokeAsync("Hello world", ct: TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Content.ShouldBe("Test response");
        result.AgentId.ShouldBe(_agent.Identity.AgentId);
        result.TokensUsed.ShouldBe(30);
        result.FinishReason.ShouldBe("stop");
    }

    [Fact]
    public async Task InvokeAsync_StoresConversationInMemory()
    {
        var chatResponse = new ChatResponse("Agent reply", "qwen2.5:72b", 5, 10, 15, "stop");
        _cognitiveClient.ChatAsync(
                Arg.Any<string>(),
                Arg.Any<IReadOnlyList<ChatMessage>>(),
                Arg.Any<CognitiveOptions>(),
                Arg.Any<CancellationToken>())
            .Returns(chatResponse);

        var context = new AgentContext { CorrelationId = "test-conv-1", UserId = "user-42" };

        await _agent.InvokeAsync("User question", context, TestContext.Current.CancellationToken);

        _memoryService.GetMessageCount("test-conv-1").ShouldBe(2); // user + assistant
        var history = _memoryService.GetConversationHistory("test-conv-1");
        history[0].Role.ShouldBe("user");
        history[0].Content.ShouldBe("User question");
        history[1].Role.ShouldBe("assistant");
        history[1].Content.ShouldBe("Agent reply");
    }

    [Fact]
    public async Task InvokeAsync_WithConversationHistory_IncludesInMessages()
    {
        var chatResponse = new ChatResponse("Response", "qwen2.5:72b", 5, 10, 15, "stop");
        IReadOnlyList<ChatMessage>? capturedMessages = null;
        _cognitiveClient.ChatAsync(
                Arg.Any<string>(),
                Arg.Do<IReadOnlyList<ChatMessage>>(msgs => capturedMessages = msgs),
                Arg.Any<CognitiveOptions>(),
                Arg.Any<CancellationToken>())
            .Returns(chatResponse);

        var context = new AgentContext
        {
            CorrelationId = "conv-history-test",
            ConversationHistory =
            [
                new ConversationMessage("user", "Previous question"),
                new ConversationMessage("assistant", "Previous answer"),
            ],
        };

        await _agent.InvokeAsync("Follow-up question", context, TestContext.Current.CancellationToken);

        capturedMessages.ShouldNotBeNull();
        // Should have history (2) + current user message (1) = 3
        capturedMessages.Count.ShouldBe(3);
        capturedMessages[0].Role.ShouldBe("user");
        capturedMessages[0].Content.ShouldBe("Previous question");
        capturedMessages[1].Role.ShouldBe("assistant");
        capturedMessages[1].Content.ShouldBe("Previous answer");
        // Last message should contain intent enrichment and the follow-up
        capturedMessages[2].Role.ShouldBe("user");
        capturedMessages[2].Content.ShouldContain("Follow-up question");
    }

    [Fact]
    public async Task InvokeAsync_EmptyInput_ThrowsArgumentException()
    {
        await Should.ThrowAsync<ArgumentException>(
            () => _agent.InvokeAsync("", ct: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task InvokeStreamingAsync_YieldsChunksAndStoresInMemory()
    {
        _cognitiveClient.ChatStreamingAsync(
                Arg.Any<string>(),
                Arg.Any<IReadOnlyList<ChatMessage>>(),
                Arg.Any<CognitiveOptions>(),
                Arg.Any<CancellationToken>())
            .Returns(CreateStreamChunks("Hello", " world", "!"));

        var context = new AgentContext { CorrelationId = "stream-conv-1" };
        var chunks = new List<AgentStreamChunk>();

        await foreach (var chunk in _agent.InvokeStreamingAsync("Stream test", context, TestContext.Current.CancellationToken))
        {
            chunks.Add(chunk);
        }

        chunks.Count.ShouldBe(3);
        chunks[0].Content.ShouldBe("Hello");
        chunks[1].Content.ShouldBe(" world");
        chunks[2].Content.ShouldBe("!");
        chunks[2].IsComplete.ShouldBeTrue(); // last chunk has FinishReason

        // Memory should contain the full response
        _memoryService.GetMessageCount("stream-conv-1").ShouldBe(2);
        var history = _memoryService.GetConversationHistory("stream-conv-1");
        history[1].Content.ShouldBe("Hello world!");
    }

    [Fact]
    public async Task EscalateIfUncertainAsync_DoesNotThrow()
    {
        // Task 31 stub — should just log and complete
        await _agent.EscalateIfUncertainAsync(0.3, "Low confidence", TestContext.Current.CancellationToken);
    }

    [Fact]
    public void Constructor_NullCognitiveClient_ThrowsArgumentNullException()
    {
        var memoryLogger = Substitute.For<ILogger<ConversationMemoryService>>();
        var agentLogger = Substitute.For<ILogger<MimirReasonerAgent>>();
        var memory = new ConversationMemoryService(memoryLogger);
        var router = new QueryRouter();

        Should.Throw<ArgumentNullException>(() =>
            new MimirReasonerAgent(null!, memory, router, agentLogger));
    }

    private static async IAsyncEnumerable<ChatStreamChunk> CreateStreamChunks(params string[] contents)
    {
        for (var i = 0; i < contents.Length; i++)
        {
            var isLast = i == contents.Length - 1;
            yield return new ChatStreamChunk(contents[i], "qwen2.5:72b", isLast ? "stop" : null);
        }

        await Task.CompletedTask;
    }
}
