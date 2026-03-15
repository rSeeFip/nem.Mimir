namespace nem.Mimir.Teams.Tests.Services;

using System.Text.Json;
using nem.Contracts.Content;
using nem.Mimir.Teams.Services;
using Shouldly;

public sealed class AdaptiveCardBuilderTests
{
    private readonly AdaptiveCardBuilder _sut = new();

    [Fact]
    public void Build_TextContent_ReturnsCardWithTextBlock()
    {
        var content = new TextContent("Hello from Mimir");

        var json = _sut.Build(content);

        json.ShouldNotBeNullOrWhiteSpace();
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.GetProperty("type").GetString()!.ShouldBe("AdaptiveCard");
        root.GetProperty("$schema").GetString()!.ShouldContain("adaptivecards.io");
        root.GetProperty("version").GetString()!.ShouldBe("1.4");

        var body = root.GetProperty("body");
        body.GetArrayLength().ShouldBeGreaterThan(0);
        body[0].GetProperty("type").GetString()!.ShouldBe("TextBlock");
        body[0].GetProperty("text").GetString()!.ShouldBe("Hello from Mimir");
    }

    [Fact]
    public void Build_TextContent_WrapsText()
    {
        var content = new TextContent("A long message that should wrap");

        var json = _sut.Build(content);
        var doc = JsonDocument.Parse(json);
        var textBlock = doc.RootElement.GetProperty("body")[0];

        textBlock.GetProperty("wrap").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public void Build_CanvasContent_RendersTitle()
    {
        var content = new CanvasContent("Report Title");

        var json = _sut.Build(content);
        var doc = JsonDocument.Parse(json);
        var body = doc.RootElement.GetProperty("body");

        body.GetArrayLength().ShouldBeGreaterThan(0);
        body[0].GetProperty("type").GetString()!.ShouldBe("TextBlock");
        body[0].GetProperty("text").GetString()!.ShouldBe("Report Title");
        body[0].GetProperty("size").GetString()!.ShouldBe("Large");
        body[0].GetProperty("weight").GetString()!.ShouldBe("Bolder");
    }

    [Fact]
    public void Build_NullContent_ReturnsEmptyCard()
    {
        var json = _sut.Build(null!);

        json.ShouldNotBeNullOrWhiteSpace();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("type").GetString()!.ShouldBe("AdaptiveCard");
        doc.RootElement.GetProperty("body").GetArrayLength().ShouldBe(1);
        doc.RootElement.GetProperty("body")[0].GetProperty("text").GetString()!.ShouldBe("(empty message)");
    }
}
