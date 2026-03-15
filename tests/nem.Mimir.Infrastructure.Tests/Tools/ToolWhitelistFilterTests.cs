namespace nem.Mimir.Infrastructure.Tests.Tools;

using nem.Mimir.Domain.McpServers;
using nem.Mimir.Domain.Tools;
using nem.Mimir.Infrastructure.Tools;
using NSubstitute;
using Shouldly;

public class ToolWhitelistFilterTests
{
    private readonly IToolProvider _inner = Substitute.For<IToolProvider>();
    private readonly IToolWhitelistService _whitelistService = Substitute.For<IToolWhitelistService>();
    private readonly Guid _serverId = Guid.NewGuid();
    private ToolWhitelistFilter CreateFilter() => new(_inner, _whitelistService, _serverId);

    [Fact]
    public async Task GetAvailableToolsAsync_filters_by_whitelist()
    {
        var tools = new List<ToolDefinition>
        {
            new("allowed_tool", "An allowed tool"),
            new("blocked_tool", "A blocked tool"),
            new("another_allowed", "Another allowed tool"),
        };
        _inner.GetAvailableToolsAsync(Arg.Any<CancellationToken>()).Returns(tools);
        _whitelistService.IsToolAllowedAsync(_serverId, "allowed_tool", Arg.Any<CancellationToken>()).Returns(true);
        _whitelistService.IsToolAllowedAsync(_serverId, "blocked_tool", Arg.Any<CancellationToken>()).Returns(false);
        _whitelistService.IsToolAllowedAsync(_serverId, "another_allowed", Arg.Any<CancellationToken>()).Returns(true);

        var filter = CreateFilter();

        var result = await filter.GetAvailableToolsAsync();

        result.Count.ShouldBe(2);
        result.Select(t => t.Name).ShouldBe(new[] { "allowed_tool", "another_allowed" });
    }

    [Fact]
    public async Task ExecuteToolAsync_blocks_non_whitelisted_tool()
    {
        _whitelistService.IsToolAllowedAsync(_serverId, "blocked_tool", Arg.Any<CancellationToken>()).Returns(false);

        var filter = CreateFilter();

        var result = await filter.ExecuteToolAsync("blocked_tool", "{}");

        result.IsSuccess.ShouldBeFalse();
        result.ErrorMessage.ShouldBe("Tool not whitelisted");
        await _inner.DidNotReceive().ExecuteToolAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteToolAsync_blocks_invalid_arguments()
    {
        _whitelistService.IsToolAllowedAsync(_serverId, "allowed_tool", Arg.Any<CancellationToken>()).Returns(true);
        _whitelistService.ValidateArguments(Arg.Any<string>()).Returns(false);

        var filter = CreateFilter();

        var result = await filter.ExecuteToolAsync("allowed_tool", """{"path": "../../../etc/passwd"}""");

        result.IsSuccess.ShouldBeFalse();
        result.ErrorMessage.ShouldBe("Argument validation failed");
        await _inner.DidNotReceive().ExecuteToolAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteToolAsync_passes_valid_whitelisted_call_to_inner()
    {
        _whitelistService.IsToolAllowedAsync(_serverId, "allowed_tool", Arg.Any<CancellationToken>()).Returns(true);
        _whitelistService.ValidateArguments(Arg.Any<string>()).Returns(true);
        _inner.ExecuteToolAsync("allowed_tool", "{}", Arg.Any<CancellationToken>())
            .Returns(ToolResult.Success("executed"));

        var filter = CreateFilter();

        var result = await filter.ExecuteToolAsync("allowed_tool", "{}");

        result.IsSuccess.ShouldBeTrue();
        result.Content.ShouldBe("executed");
        await _inner.Received(1).ExecuteToolAsync("allowed_tool", "{}", Arg.Any<CancellationToken>());
    }
}
