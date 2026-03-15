using nem.Mimir.Domain.McpServers;
using nem.Mimir.Domain.Tools;
using nem.Mimir.Infrastructure.Tools;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;

namespace nem.Mimir.Infrastructure.Tests.Tools;

public sealed class AuditingToolProviderDecoratorTests
{
    private readonly IToolProvider _inner = Substitute.For<IToolProvider>();
    private readonly IToolAuditLogger _auditLogger = Substitute.For<IToolAuditLogger>();
    private readonly AuditingToolProviderDecorator _sut;

    public AuditingToolProviderDecoratorTests()
    {
        _sut = new AuditingToolProviderDecorator(_inner, _auditLogger);
    }

    [Fact]
    public async Task GetAvailableToolsAsync_DelegatesToInner()
    {
        var expected = new List<ToolDefinition> { new("test-tool", "desc") }.AsReadOnly();
        _inner.GetAvailableToolsAsync(Arg.Any<CancellationToken>()).Returns(expected);

        var result = await _sut.GetAvailableToolsAsync();

        result.ShouldBeSameAs(expected);
        await _inner.Received(1).GetAvailableToolsAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteToolAsync_LogsSuccessfulExecution()
    {
        _inner.ExecuteToolAsync("my-tool", "{}", Arg.Any<CancellationToken>())
            .Returns(ToolResult.Success("ok"));

        var result = await _sut.ExecuteToolAsync("my-tool", "{}");

        result.IsSuccess.ShouldBeTrue();
        result.Content.ShouldBe("ok");

        await _auditLogger.Received(1).LogToolExecutionAsync(
            Arg.Is<McpToolAuditLog>(log =>
                log.ToolName == "my-tool" &&
                log.Input == "{}" &&
                log.Output == "ok" &&
                log.Success &&
                log.ErrorMessage == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteToolAsync_LogsFailedExecution()
    {
        _inner.ExecuteToolAsync("bad-tool", "{}", Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("boom"));

        await Should.ThrowAsync<InvalidOperationException>(() =>
            _sut.ExecuteToolAsync("bad-tool", "{}"));

        await _auditLogger.Received(1).LogToolExecutionAsync(
            Arg.Is<McpToolAuditLog>(log =>
                log.ToolName == "bad-tool" &&
                !log.Success &&
                log.ErrorMessage == "boom"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteToolAsync_MeasuresLatency()
    {
        _inner.ExecuteToolAsync("slow-tool", "{}", Arg.Any<CancellationToken>())
            .Returns(async _ =>
            {
                await Task.Delay(50);
                return ToolResult.Success("done");
            });

        await _sut.ExecuteToolAsync("slow-tool", "{}");

        await _auditLogger.Received(1).LogToolExecutionAsync(
            Arg.Is<McpToolAuditLog>(log => log.LatencyMs >= 0),
            Arg.Any<CancellationToken>());
    }
}
