using System.Diagnostics;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Mappings;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Application.Conversations.Commands;
using nem.Mimir.Domain.Entities;
using nem.Mimir.Domain.Tools;
using NSubstitute;
using Shouldly;

namespace nem.Mimir.Application.Tests.Conversations.Commands;

public sealed class ParallelToolExecutionTests
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

    public ParallelToolExecutionTests()
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
            .Returns(new List<LlmMessage> { new("user", "test") });

        _contextWindowService.EstimateTokenCount(Arg.Any<string>())
            .Returns(10);
    }

    [Fact]
    public async Task MultipleToolCalls_ExecuteConcurrently_NotSequentially()
    {
        const int toolDelayMs = 200;
        const int toolCount = 3;

        SetupToolProvider(
            new ToolDefinition("tool1", "Tool 1"),
            new ToolDefinition("tool2", "Tool 2"),
            new ToolDefinition("tool3", "Tool 3"));

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
                            new LlmToolCall("c1", "tool1", "{}"),
                            new LlmToolCall("c2", "tool2", "{}"),
                            new LlmToolCall("c3", "tool3", "{}"),
                        },
                    };
                }

                return new LlmResponse("done", "phi-4-mini", 20, 10, 30, "stop");
            });

        _toolProvider.ExecuteToolAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(async _ =>
            {
                await Task.Delay(toolDelayMs);
                return ToolResult.Success("result");
            });

        var handler = CreateHandler();
        var sw = Stopwatch.StartNew();
        await handler.Handle(new SendMessageCommand(_conversationId, "run tools", null), CancellationToken.None);
        sw.Stop();

        var sequentialMs = toolDelayMs * toolCount;
        sw.ElapsedMilliseconds.ShouldBeLessThan(sequentialMs,
            $"Expected parallel execution (<{sequentialMs}ms) but took {sw.ElapsedMilliseconds}ms");

        await _toolProvider.Received(toolCount).ExecuteToolAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SingleToolCall_ExecutesCorrectly()
    {
        SetupToolProvider(new ToolDefinition("search", "Search"));
        SetupConversation();

        var callCount = 0;
        _llmService.SendMessageAsync(
                Arg.Any<string>(), Arg.Any<IReadOnlyList<LlmMessage>>(),
                Arg.Any<IReadOnlyList<ToolDefinition>?>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                callCount++;
                return callCount == 1
                    ? new LlmResponse("", "phi-4-mini", 10, 5, 15, "tool_calls")
                    {
                        ToolCalls = new[] { new LlmToolCall("c1", "search", "{\"q\":\"test\"}") },
                    }
                    : new LlmResponse("found it", "phi-4-mini", 20, 10, 30, "stop");
            });

        _toolProvider.ExecuteToolAsync("search", "{\"q\":\"test\"}", Arg.Any<CancellationToken>())
            .Returns(ToolResult.Success("result"));

        var handler = CreateHandler();
        var result = await handler.Handle(
            new SendMessageCommand(_conversationId, "search test", null), CancellationToken.None);

        result.Content.ShouldBe("found it");
        await _toolProvider.Received(1).ExecuteToolAsync("search", "{\"q\":\"test\"}", Arg.Any<CancellationToken>());
    }

    private SendMessageCommandHandler CreateHandler() =>
        new(_repository, _currentUserService, _unitOfWork, _llmService, _contextWindowService, _mapper, _toolProvider);

    private void SetupConversation()
    {
        var conversation = Conversation.Create(_userId, "Test Chat");
        var prop = typeof(Conversation).BaseType!.BaseType!.GetProperty("Id");
        prop!.SetValue(conversation, _conversationId);
        _repository.GetWithMessagesAsync(_conversationId, Arg.Any<CancellationToken>())
            .Returns(conversation);
    }

    private void SetupToolProvider(params ToolDefinition[] tools)
    {
        _toolProvider.GetAvailableToolsAsync(Arg.Any<CancellationToken>())
            .Returns(tools.ToList());
    }
}
