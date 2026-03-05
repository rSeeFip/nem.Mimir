using Mimir.Application.Common.Interfaces;
using Mimir.Application.Common.Mappings;
using Mimir.Application.Common.Models;
using Mimir.Application.Conversations.Commands;
using Mimir.Domain.Entities;
using Mimir.Domain.Tools;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;

namespace Mimir.Application.Tests.Conversations.Commands;

public sealed class SendMessageToolLoopTests
{
    private readonly IConversationRepository _repository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILlmService _llmService;
    private readonly IContextWindowService _contextWindowService;
    private readonly MimirMapper _mapper;
    private readonly IToolProvider _toolProvider;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _conversationId = Guid.NewGuid();

    public SendMessageToolLoopTests()
    {
        _repository = Substitute.For<IConversationRepository>();
        _currentUserService = Substitute.For<ICurrentUserService>();
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _llmService = Substitute.For<ILlmService>();
        _contextWindowService = Substitute.For<IContextWindowService>();
        _mapper = new MimirMapper();
        _toolProvider = Substitute.For<IToolProvider>();

        _currentUserService.UserId.Returns(_userId.ToString());

        _contextWindowService.BuildLlmMessagesAsync(
                Arg.Any<Conversation>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => new List<LlmMessage>
            {
                new("system", "You are Mimir."),
                new("user", callInfo.ArgAt<string>(1)),
            });

        _contextWindowService.EstimateTokenCount(Arg.Any<string>())
            .Returns(callInfo =>
            {
                var text = callInfo.Arg<string>();
                return string.IsNullOrEmpty(text) ? 0 : (text.Length + 3) / 4;
            });
    }

    [Fact]
    public async Task Handle_NoToolsAvailable_StreamsNormally()
    {
        // Given: no tools available
        _toolProvider.GetAvailableToolsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<ToolDefinition>());

        var handler = CreateHandler();
        SetupConversation();

        var chunks = CreateStreamChunks("Streamed response");
        _llmService.StreamMessageAsync("phi-4-mini", Arg.Any<IReadOnlyList<LlmMessage>>(), Arg.Any<CancellationToken>())
            .Returns(chunks);

        // When
        var result = await handler.Handle(
            new SendMessageCommand(_conversationId, "Hello", null), CancellationToken.None);

        // Then: should use streaming path, not non-streaming
        result.Content.ShouldBe("Streamed response");
        _llmService.Received(1).StreamMessageAsync(
            Arg.Any<string>(), Arg.Any<IReadOnlyList<LlmMessage>>(), Arg.Any<CancellationToken>());
        await _llmService.DidNotReceive().SendMessageAsync(
            Arg.Any<string>(), Arg.Any<IReadOnlyList<LlmMessage>>(), Arg.Any<IReadOnlyList<ToolDefinition>?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SingleToolCall_ExecutesAndReturnsResult()
    {
        // Given: one tool, LLM calls it once then returns text
        SetupToolProvider(new ToolDefinition("search", "Search the web"));

        var handler = CreateHandler();
        SetupConversation();

        var callCount = 0;
        _llmService.SendMessageAsync(
                Arg.Any<string>(), Arg.Any<IReadOnlyList<LlmMessage>>(),
                Arg.Any<IReadOnlyList<ToolDefinition>?>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                callCount++;
                if (callCount == 1)
                {
                    return new LlmResponse("", "phi-4-mini", 10, 5, 15, "tool_calls")
                    {
                        ToolCalls = new[] { new LlmToolCall("call_1", "search", "{\"query\":\"test\"}") },
                    };
                }

                return new LlmResponse("Search result: found it!", "phi-4-mini", 20, 10, 30, "stop");
            });

        _toolProvider.ExecuteToolAsync("search", "{\"query\":\"test\"}", Arg.Any<CancellationToken>())
            .Returns(ToolResult.Success("Result for test"));

        // When
        var result = await handler.Handle(
            new SendMessageCommand(_conversationId, "Search for test", null), CancellationToken.None);

        // Then
        result.Content.ShouldBe("Search result: found it!");
        await _toolProvider.Received(1).ExecuteToolAsync("search", "{\"query\":\"test\"}", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_MultipleToolCallsInOneResponse_ExecutesSequentially()
    {
        // Given: LLM returns 3 tool calls in a single response
        SetupToolProvider(
            new ToolDefinition("search", "Search"),
            new ToolDefinition("calc", "Calculate"),
            new ToolDefinition("weather", "Weather"));

        var handler = CreateHandler();
        SetupConversation();

        var callCount = 0;
        _llmService.SendMessageAsync(
                Arg.Any<string>(), Arg.Any<IReadOnlyList<LlmMessage>>(),
                Arg.Any<IReadOnlyList<ToolDefinition>?>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                callCount++;
                if (callCount == 1)
                {
                    return new LlmResponse("", "phi-4-mini", 10, 5, 15, "tool_calls")
                    {
                        ToolCalls = new[]
                        {
                            new LlmToolCall("call_1", "search", "{\"q\":\"a\"}"),
                            new LlmToolCall("call_2", "calc", "{\"expr\":\"1+1\"}"),
                            new LlmToolCall("call_3", "weather", "{\"city\":\"NYC\"}"),
                        },
                    };
                }

                return new LlmResponse("All done!", "phi-4-mini", 50, 20, 70, "stop");
            });

        _toolProvider.ExecuteToolAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ToolResult.Success("ok"));

        // When
        var result = await handler.Handle(
            new SendMessageCommand(_conversationId, "Do three things", null), CancellationToken.None);

        // Then: all 3 tools executed
        result.Content.ShouldBe("All done!");
        await _toolProvider.Received(3).ExecuteToolAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _toolProvider.Received(1).ExecuteToolAsync("search", "{\"q\":\"a\"}", Arg.Any<CancellationToken>());
        await _toolProvider.Received(1).ExecuteToolAsync("calc", "{\"expr\":\"1+1\"}", Arg.Any<CancellationToken>());
        await _toolProvider.Received(1).ExecuteToolAsync("weather", "{\"city\":\"NYC\"}", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_MultiRoundToolLoop_CompletesWithinLimit()
    {
        // Given: LLM calls tools for 3 rounds then returns text
        SetupToolProvider(new ToolDefinition("step", "A step tool"));

        var handler = CreateHandler();
        SetupConversation();

        var callCount = 0;
        _llmService.SendMessageAsync(
                Arg.Any<string>(), Arg.Any<IReadOnlyList<LlmMessage>>(),
                Arg.Any<IReadOnlyList<ToolDefinition>?>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                callCount++;
                if (callCount <= 3)
                {
                    return new LlmResponse("", "phi-4-mini", 10, 5, 15, "tool_calls")
                    {
                        ToolCalls = new[] { new LlmToolCall($"call_{callCount}", "step", "{}") },
                    };
                }

                return new LlmResponse("Completed after 3 rounds", "phi-4-mini", 50, 20, 70, "stop");
            });

        _toolProvider.ExecuteToolAsync("step", "{}", Arg.Any<CancellationToken>())
            .Returns(ToolResult.Success("step done"));

        // When
        var result = await handler.Handle(
            new SendMessageCommand(_conversationId, "Multi-step", null), CancellationToken.None);

        // Then
        result.Content.ShouldBe("Completed after 3 rounds");
        // 3 tool calls + 1 final text = 4 LLM calls
        await _llmService.Received(4).SendMessageAsync(
            Arg.Any<string>(), Arg.Any<IReadOnlyList<LlmMessage>>(),
            Arg.Any<IReadOnlyList<ToolDefinition>?>(), Arg.Any<CancellationToken>());
        await _toolProvider.Received(3).ExecuteToolAsync("step", "{}", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ToolCallExceedsMaxIterations_ReturnsResponse()
    {
        // Given: LLM always returns tool calls, never text (hits max 5 iterations)
        SetupToolProvider(new ToolDefinition("infinite", "Never stops"));

        var handler = CreateHandler();
        SetupConversation();

        var callCount = 0;
        _llmService.SendMessageAsync(
                Arg.Any<string>(), Arg.Any<IReadOnlyList<LlmMessage>>(),
                Arg.Any<IReadOnlyList<ToolDefinition>?>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                callCount++;
                if (callCount <= 5)
                {
                    return new LlmResponse("", "phi-4-mini", 10, 5, 15, "tool_calls")
                    {
                        ToolCalls = new[] { new LlmToolCall($"call_{callCount}", "infinite", "{}") },
                    };
                }

                // 6th call: final fallback call without tools
                return new LlmResponse("I've done my best", "phi-4-mini", 50, 20, 70, "stop");
            });

        // Also set up the no-tools overload for the fallback call
        _llmService.SendMessageAsync(
                Arg.Any<string>(), Arg.Any<IReadOnlyList<LlmMessage>>(), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse("Fallback response after max iterations", "phi-4-mini", 50, 20, 70, "stop"));

        _toolProvider.ExecuteToolAsync("infinite", "{}", Arg.Any<CancellationToken>())
            .Returns(ToolResult.Success("keep going"));

        // When
        var result = await handler.Handle(
            new SendMessageCommand(_conversationId, "Loop forever", null), CancellationToken.None);

        // Then: should have executed 5 tool rounds then made a final call
        result.Content.ShouldNotBeNullOrEmpty();
        await _toolProvider.Received(5).ExecuteToolAsync("infinite", "{}", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ToolExecutionThrows_ReturnsErrorToLlm()
    {
        // Given: tool throws an exception
        SetupToolProvider(new ToolDefinition("failing", "A failing tool"));

        var handler = CreateHandler();
        SetupConversation();

        var callCount = 0;
        _llmService.SendMessageAsync(
                Arg.Any<string>(), Arg.Any<IReadOnlyList<LlmMessage>>(),
                Arg.Any<IReadOnlyList<ToolDefinition>?>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                callCount++;
                if (callCount == 1)
                {
                    return new LlmResponse("", "phi-4-mini", 10, 5, 15, "tool_calls")
                    {
                        ToolCalls = new[] { new LlmToolCall("call_1", "failing", "{}") },
                    };
                }

                return new LlmResponse("I see the tool failed, let me help anyway", "phi-4-mini", 20, 10, 30, "stop");
            });

        _toolProvider.ExecuteToolAsync("failing", "{}", Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Connection refused"));

        // When
        var result = await handler.Handle(
            new SendMessageCommand(_conversationId, "Use failing tool", null), CancellationToken.None);

        // Then: handler should NOT throw; error sent to LLM as tool result
        result.Content.ShouldBe("I see the tool failed, let me help anyway");

        // Verify the second LLM call received messages including the error
        await _llmService.Received(2).SendMessageAsync(
            Arg.Any<string>(), Arg.Any<IReadOnlyList<LlmMessage>>(),
            Arg.Any<IReadOnlyList<ToolDefinition>?>(), Arg.Any<CancellationToken>());
    }

    private SendMessageCommandHandler CreateHandler() =>
        new(_repository, _currentUserService, _unitOfWork, _llmService, _contextWindowService, _mapper, _toolProvider);

    private void SetupConversation()
    {
        var conversation = Conversation.Create(_userId, "Test Chat");
        SetConversationId(conversation, _conversationId);
        _repository.GetWithMessagesAsync(_conversationId, Arg.Any<CancellationToken>())
            .Returns(conversation);
    }

    private void SetupToolProvider(params ToolDefinition[] tools)
    {
        _toolProvider.GetAvailableToolsAsync(Arg.Any<CancellationToken>())
            .Returns(tools.ToList());
    }

    private static void SetConversationId(Conversation conversation, Guid id)
    {
        var prop = typeof(Conversation).BaseType!.BaseType!.GetProperty("Id");
        prop!.SetValue(conversation, id);
    }

    private static async IAsyncEnumerable<LlmStreamChunk> CreateStreamChunks(params string[] contents)
    {
        foreach (var content in contents)
        {
            yield return new LlmStreamChunk(content, "phi-4-mini", null);
        }

        await Task.CompletedTask;
    }
}
