using NSubstitute;
using Shouldly;
using Wolverine;
using nem.Mimir.Application.CodeExecution.Commands;
using nem.Mimir.McpServer.Tools;

namespace nem.Mimir.McpServer.Tests.Tools;

public sealed class MimirExecuteCodeToolTests
{
    private readonly IMessageBus _messageBus = Substitute.For<IMessageBus>();

    [Fact]
    public async Task ExecuteCodeAsync_SendsCommandAndReturnsResult()
    {
        var conversationId = Guid.NewGuid();
        var expected = new CodeExecutionResultDto("Hello World", "", 0, 150, false);

        _messageBus
            .InvokeAsync<CodeExecutionResultDto>(Arg.Any<ExecuteCodeCommand>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        var result = await MimirExecuteCodeTool.ExecuteCodeAsync(
            "python", "print('Hello World')", conversationId, _messageBus, CancellationToken.None);

        result.ShouldNotBeNullOrWhiteSpace();
        result.ShouldContain("Hello World");

        await _messageBus.Received(1)
            .InvokeAsync<CodeExecutionResultDto>(
                Arg.Is<ExecuteCodeCommand>(c =>
                    c.Language == "python" &&
                    c.Code == "print('Hello World')" &&
                    c.ConversationId == conversationId),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteCodeAsync_TimedOut_ReturnsTimedOutResult()
    {
        var conversationId = Guid.NewGuid();
        var expected = new CodeExecutionResultDto("", "Timed out", 137, 30000, true);

        _messageBus
            .InvokeAsync<CodeExecutionResultDto>(Arg.Any<ExecuteCodeCommand>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        var result = await MimirExecuteCodeTool.ExecuteCodeAsync(
            "javascript", "while(true){}", conversationId, _messageBus, CancellationToken.None);

        result.ShouldContain("true");
    }
}
