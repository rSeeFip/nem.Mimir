namespace Mimir.Teams.Tests.Services;

using Microsoft.Bot.Schema;
using nem.Contracts.Content;
using Mimir.Teams.Services;
using Shouldly;

public sealed class ActivityConverterTests
{
    private readonly ActivityConverter _sut = new();

    [Fact]
    public void Convert_TextMessage_ReturnsTextContent()
    {
        var activity = new Activity
        {
            Type = ActivityTypes.Message,
            Text = "Hello from Teams",
            Timestamp = DateTimeOffset.UtcNow,
        };

        var result = _sut.Convert(activity);

        result.ShouldBeOfType<TextContent>();
        var text = (TextContent)result;
        text.Text.ShouldBe("Hello from Teams");
    }

    [Fact]
    public void Convert_NullText_ReturnsTextContentWithEmpty()
    {
        var activity = new Activity
        {
            Type = ActivityTypes.Message,
            Text = null,
            Timestamp = DateTimeOffset.UtcNow,
        };

        var result = _sut.Convert(activity);

        result.ShouldBeOfType<TextContent>();
        ((TextContent)result).Text.ShouldBe(string.Empty);
    }

    [Fact]
    public void Convert_HtmlText_SetsFormatToHtml()
    {
        var activity = new Activity
        {
            Type = ActivityTypes.Message,
            Text = "<b>Bold</b> text",
            TextFormat = TextFormatTypes.Xml,
            Timestamp = DateTimeOffset.UtcNow,
        };

        var result = _sut.Convert(activity);

        var text = result.ShouldBeOfType<TextContent>();
        text.Format.ShouldBe("xml");
    }

    [Fact]
    public void Convert_PlainText_SetsFormatToPlain()
    {
        var activity = new Activity
        {
            Type = ActivityTypes.Message,
            Text = "plain text",
            TextFormat = TextFormatTypes.Plain,
            Timestamp = DateTimeOffset.UtcNow,
        };

        var result = _sut.Convert(activity);

        var text = result.ShouldBeOfType<TextContent>();
        text.Format.ShouldBe("plain");
    }

    [Fact]
    public void Convert_SetsCreatedAtFromTimestamp()
    {
        var ts = new DateTimeOffset(2025, 3, 6, 12, 0, 0, TimeSpan.Zero);
        var activity = new Activity
        {
            Type = ActivityTypes.Message,
            Text = "test",
            Timestamp = ts,
        };

        var result = _sut.Convert(activity);

        result.CreatedAt.ShouldBe(ts);
    }

    [Fact]
    public void Convert_NullTimestamp_UsesUtcNow()
    {
        var before = DateTimeOffset.UtcNow;
        var activity = new Activity
        {
            Type = ActivityTypes.Message,
            Text = "test",
            Timestamp = null,
        };

        var result = _sut.Convert(activity);

        result.CreatedAt.ShouldNotBeNull();
        result.CreatedAt!.Value.ShouldBeGreaterThanOrEqualTo(before);
    }
}
