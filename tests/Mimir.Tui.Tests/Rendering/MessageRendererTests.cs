using Mimir.Tui.Rendering;
using Shouldly;

namespace Mimir.Tui.Tests.Rendering;

public sealed class MessageRendererTests
{
    [Fact]
    public void FormatMarkdown_EmptyString_ReturnsEmpty()
    {
        var result = MessageRenderer.FormatMarkdown("");

        result.ShouldBe(string.Empty);
    }

    [Fact]
    public void FormatMarkdown_Null_ReturnsEmpty()
    {
        var result = MessageRenderer.FormatMarkdown(null!);

        result.ShouldBe(string.Empty);
    }

    [Fact]
    public void FormatMarkdown_PlainText_ReturnsEscapedText()
    {
        var result = MessageRenderer.FormatMarkdown("Hello world");

        result.ShouldBe("Hello world");
    }

    [Fact]
    public void FormatMarkdown_BoldText_ReturnsSpectreMarkup()
    {
        var result = MessageRenderer.FormatMarkdown("This is **bold** text");

        result.ShouldBe("This is [bold]bold[/] text");
    }

    [Fact]
    public void FormatMarkdown_ItalicText_ReturnsSpectreMarkup()
    {
        var result = MessageRenderer.FormatMarkdown("This is *italic* text");

        result.ShouldBe("This is [italic]italic[/] text");
    }

    [Fact]
    public void FormatMarkdown_InlineCode_ReturnsSpectreMarkup()
    {
        var result = MessageRenderer.FormatMarkdown("Use `Console.WriteLine` here");

        result.ShouldBe("Use [grey on black]Console.WriteLine[/] here");
    }

    [Fact]
    public void FormatMarkdown_CodeBlock_RendersWithDimSeparators()
    {
        var input = "Before\n```csharp\nvar x = 1;\n```\nAfter";

        var result = MessageRenderer.FormatMarkdown(input);

        result.ShouldContain("[dim]───────────────────[/]");
        result.ShouldContain("[grey]var x = 1;[/]");
        result.ShouldContain("Before");
        result.ShouldContain("After");
    }

    [Fact]
    public void FormatMarkdown_SpectreMarkupCharacters_AreEscaped()
    {
        var result = MessageRenderer.FormatMarkdown("Use [red] markup");

        result.ShouldContain("[[red]]");
    }

    [Fact]
    public void FormatMarkdown_BoldAndItalicCombined_BothFormatted()
    {
        var result = MessageRenderer.FormatMarkdown("**bold** and *italic*");

        result.ShouldContain("[bold]bold[/]");
        result.ShouldContain("[italic]italic[/]");
    }

    [Fact]
    public void FormatMarkdown_MultipleInlineCode_AllFormatted()
    {
        var result = MessageRenderer.FormatMarkdown("`foo` and `bar`");

        result.ShouldContain("[grey on black]foo[/]");
        result.ShouldContain("[grey on black]bar[/]");
    }

    [Fact]
    public void FormatMarkdown_CodeBlockContentNotFormattedAsMarkdown()
    {
        var input = "```\n**not bold**\n```";

        var result = MessageRenderer.FormatMarkdown(input);

        // Inside code block, **bold** should be escaped, not formatted
        result.ShouldNotContain("[bold]");
        result.ShouldContain("[grey]");
    }
}
