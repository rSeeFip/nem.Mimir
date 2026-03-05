using Mimir.Domain.Tools;
using Mimir.Infrastructure.Tools;
using NSubstitute;
using Shouldly;

namespace Mimir.Infrastructure.Tests.Tools;

public sealed class CompositeToolProviderTests
{
    [Fact]
    public async Task GetAvailableToolsAsync_AggregatesToolsFromMultipleProviders()
    {
        var provider1 = Substitute.For<IToolProvider>();
        provider1.GetAvailableToolsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<ToolDefinition>
            {
                new("tool-a", "Tool A"),
            }.AsReadOnly());

        var provider2 = Substitute.For<IToolProvider>();
        provider2.GetAvailableToolsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<ToolDefinition>
            {
                new("tool-b", "Tool B"),
            }.AsReadOnly());

        var sut = new CompositeToolProvider(new[] { provider1, provider2 });

        var tools = await sut.GetAvailableToolsAsync();

        tools.Count.ShouldBe(2);
        tools.ShouldContain(t => t.Name == "tool-a");
        tools.ShouldContain(t => t.Name == "tool-b");
    }

    [Fact]
    public async Task GetAvailableToolsAsync_DeduplicatesByName_KeepsFirstOccurrence()
    {
        var provider1 = Substitute.For<IToolProvider>();
        provider1.GetAvailableToolsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<ToolDefinition>
            {
                new("shared-tool", "From Provider 1"),
            }.AsReadOnly());

        var provider2 = Substitute.For<IToolProvider>();
        provider2.GetAvailableToolsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<ToolDefinition>
            {
                new("shared-tool", "From Provider 2"),
            }.AsReadOnly());

        var sut = new CompositeToolProvider(new[] { provider1, provider2 });

        var tools = await sut.GetAvailableToolsAsync();

        tools.Count.ShouldBe(1);
        tools[0].Description.ShouldBe("From Provider 1");
    }

    [Fact]
    public async Task GetAvailableToolsAsync_EmptyProviderList_ReturnsEmpty()
    {
        var sut = new CompositeToolProvider(Array.Empty<IToolProvider>());

        var tools = await sut.GetAvailableToolsAsync();

        tools.ShouldBeEmpty();
    }

    [Fact]
    public async Task ExecuteToolAsync_RoutesToCorrectProvider()
    {
        var provider1 = Substitute.For<IToolProvider>();
        provider1.GetAvailableToolsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<ToolDefinition>
            {
                new("tool-a", "Tool A"),
            }.AsReadOnly());
        provider1.ExecuteToolAsync("tool-a", "{}", Arg.Any<CancellationToken>())
            .Returns(ToolResult.Success("result from A"));

        var provider2 = Substitute.For<IToolProvider>();
        provider2.GetAvailableToolsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<ToolDefinition>
            {
                new("tool-b", "Tool B"),
            }.AsReadOnly());

        var sut = new CompositeToolProvider(new[] { provider1, provider2 });

        var result = await sut.ExecuteToolAsync("tool-a", "{}");

        result.IsSuccess.ShouldBeTrue();
        result.Content.ShouldBe("result from A");
        await provider1.Received(1).ExecuteToolAsync("tool-a", "{}", Arg.Any<CancellationToken>());
        await provider2.DidNotReceive().ExecuteToolAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteToolAsync_ToolNotFound_ReturnsFailure()
    {
        var provider = Substitute.For<IToolProvider>();
        provider.GetAvailableToolsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<ToolDefinition>().AsReadOnly());

        var sut = new CompositeToolProvider(new[] { provider });

        var result = await sut.ExecuteToolAsync("nonexistent", "{}");

        result.IsSuccess.ShouldBeFalse();
        result.ErrorMessage!.ShouldContain("not found");
    }

    [Fact]
    public async Task ExecuteToolAsync_EmptyProviders_ReturnsFailure()
    {
        var sut = new CompositeToolProvider(Array.Empty<IToolProvider>());

        var result = await sut.ExecuteToolAsync("any-tool", "{}");

        result.IsSuccess.ShouldBeFalse();
        result.ErrorMessage!.ShouldContain("not found");
    }
}
