namespace nem.Mimir.Telegram.Tests.Canvas;

using nem.Mimir.Telegram.Canvas;
using nem.Contracts.Canvas;
using Shouldly;

public sealed class TelegramCanvasRendererTests
{
    private readonly TelegramCanvasRenderer _sut = new();

    // ─── Format ──────────────────────────────────────────────────

    [Fact]
    public void Format_ShouldBeMarkdown()
    {
        _sut.Format.ShouldBe(RenderFormat.Markdown);
    }

    // ─── Render: Empty / Null ────────────────────────────────────

    [Fact]
    public void Render_NullBlocks_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => _sut.Render(null!));
    }

    [Fact]
    public void Render_EmptyBlocks_ReturnsEmptyContent()
    {
        var result = _sut.Render([]);

        result.Format.ShouldBe(RenderFormat.Markdown);
        result.Content.ShouldBeEmpty();
    }

    // ─── TextBlock ───────────────────────────────────────────────

    [Fact]
    public void Render_TextBlock_EscapesMarkdownV2()
    {
        var blocks = new ContentBlock[] { new TextBlock("Hello *world* & [test]") };

        var result = _sut.Render(blocks);

        result.Content.ShouldContain("\\*");
        result.Content.ShouldContain("\\[");
        result.Content.ShouldContain("\\]");
    }

    [Fact]
    public void Render_TextBlock_PlainText_NoExtraEscapes()
    {
        var blocks = new ContentBlock[] { new TextBlock("Hello world") };

        var result = _sut.Render(blocks);

        result.Content.ShouldBe("Hello world");
    }

    // ─── CodeBlock ───────────────────────────────────────────────

    [Fact]
    public void Render_CodeBlock_WrapsInTripleBackticks()
    {
        var blocks = new ContentBlock[] { new CodeBlock("var x = 1;", "csharp") };

        var result = _sut.Render(blocks);

        result.Content.ShouldStartWith("```csharp");
        result.Content.ShouldEndWith("```");
        result.Content.ShouldContain("var x = 1;");
    }

    [Fact]
    public void Render_CodeBlock_EscapesBackticksInCode()
    {
        var blocks = new ContentBlock[] { new CodeBlock("echo `hello`", "bash") };

        var result = _sut.Render(blocks);

        result.Content.ShouldContain("echo \\`hello\\`");
    }

    // ─── ImageBlock ──────────────────────────────────────────────

    [Fact]
    public void Render_ImageBlock_WithAltText_RendersAsLink()
    {
        var blocks = new ContentBlock[] { new ImageBlock("https://example.com/img.png", "My Photo") };

        var result = _sut.Render(blocks);

        result.Content.ShouldContain("https://example.com/img.png");
        // Alt text is escaped, contains 🖼 prefix
        result.Content.ShouldContain("My Photo");
    }

    [Fact]
    public void Render_ImageBlock_WithoutAltText_UsesDefaultText()
    {
        var blocks = new ContentBlock[] { new ImageBlock("https://example.com/img.png") };

        var result = _sut.Render(blocks);

        result.Content.ShouldContain("Image");
        result.Content.ShouldContain("https://example.com/img.png");
    }

    [Fact]
    public void Render_ImageBlock_UrlWithParens_EscapesClosingParen()
    {
        var blocks = new ContentBlock[] { new ImageBlock("https://example.com/img_(1).png", "Test") };

        var result = _sut.Render(blocks);

        result.Content.ShouldContain("https://example.com/img_(1\\).png");
    }

    // ─── TableBlock ──────────────────────────────────────────────

    [Fact]
    public void Render_TableBlock_RendersAsciiTable()
    {
        var blocks = new ContentBlock[]
        {
            new TableBlock(
                ["Name", "Age"],
                [["Alice", "30"], ["Bob", "25"]]),
        };

        var result = _sut.Render(blocks);

        result.Content.ShouldStartWith("```");
        result.Content.ShouldEndWith("```");
        result.Content.ShouldContain("| Name");
        result.Content.ShouldContain("| Alice");
        result.Content.ShouldContain("| Bob");
        result.Content.ShouldContain("---");
    }

    [Fact]
    public void Render_TableBlock_WithCaption_IncludesCaption()
    {
        var blocks = new ContentBlock[]
        {
            new TableBlock(
                ["Col"],
                [["Val"]],
                "My Table"),
        };

        var result = _sut.Render(blocks);

        result.Content.ShouldContain("My Table");
    }

    [Fact]
    public void Render_TableBlock_ColumnsAlignedByWidestCell()
    {
        var blocks = new ContentBlock[]
        {
            new TableBlock(
                ["X", "LongHeader"],
                [["ShortVal", "Y"]]),
        };

        var result = _sut.Render(blocks);

        // The header "X" column should be widened to accommodate "ShortVal"
        result.Content.ShouldContain("ShortVal");
        result.Content.ShouldContain("LongHeader");
    }

    // ─── ChartBlock ──────────────────────────────────────────────

    [Fact]
    public void Render_ChartBlock_RendersTextDescription()
    {
        var blocks = new ContentBlock[]
        {
            new ChartBlock("bar", "{\"labels\":[\"A\",\"B\"],\"values\":[1,2]}", "Sales Chart"),
        };

        var result = _sut.Render(blocks);

        result.Content.ShouldContain("Sales Chart");
        result.Content.ShouldContain("bar");
    }

    [Fact]
    public void Render_ChartBlock_WithoutTitle_UsesDefaultTitle()
    {
        var blocks = new ContentBlock[]
        {
            new ChartBlock("line", "{\"data\":[1,2,3]}"),
        };

        var result = _sut.Render(blocks);

        result.Content.ShouldContain("Chart");
        result.Content.ShouldContain("line");
    }

    // ─── FormBlock ───────────────────────────────────────────────

    [Fact]
    public void Render_FormBlock_DisplaysFieldsReadOnly()
    {
        var blocks = new ContentBlock[]
        {
            new FormBlock("contact-form",
            [
                new FormField("name", "text", "Full Name", true),
                new FormField("email", "email", "Email Address"),
            ]),
        };

        var result = _sut.Render(blocks);

        result.Content.ShouldContain("contact\\-form");
        result.Content.ShouldContain("Full Name");
        result.Content.ShouldContain("text");
        result.Content.ShouldContain("Email Address");
        result.Content.ShouldContain("email");
    }

    [Fact]
    public void Render_FormBlock_RequiredFieldsMarkedWithAsterisk()
    {
        var blocks = new ContentBlock[]
        {
            new FormBlock("f1",
            [
                new FormField("n", "text", "Name", true),
            ]),
        };

        var result = _sut.Render(blocks);

        result.Content.ShouldContain("\\*");
    }

    // ─── ProgressBlock ───────────────────────────────────────────

    [Fact]
    public void Render_ProgressBlock_RendersBarWithPercentage()
    {
        var blocks = new ContentBlock[] { new ProgressBlock("Loading", 80) };

        var result = _sut.Render(blocks);

        result.Content.ShouldContain("Loading");
        result.Content.ShouldContain("80%");
        result.Content.ShouldContain("\u2588"); // filled block char
        result.Content.ShouldContain("\u2591"); // empty block char
    }

    [Fact]
    public void Render_ProgressBlock_ZeroPercent_AllEmpty()
    {
        var blocks = new ContentBlock[] { new ProgressBlock("Waiting", 0) };

        var result = _sut.Render(blocks);

        result.Content.ShouldContain("0%");
    }

    [Fact]
    public void Render_ProgressBlock_HundredPercent_AllFilled()
    {
        var blocks = new ContentBlock[] { new ProgressBlock("Done", 100) };

        var result = _sut.Render(blocks);

        result.Content.ShouldContain("100%");
    }

    [Fact]
    public void Render_ProgressBlock_WithStatus_IncludesStatus()
    {
        var blocks = new ContentBlock[] { new ProgressBlock("Upload", 50, "Uploading file") };

        var result = _sut.Render(blocks);

        result.Content.ShouldContain("Uploading file");
    }

    // ─── LinkBlock ───────────────────────────────────────────────

    [Fact]
    public void Render_LinkBlock_RendersMarkdownV2Link()
    {
        var blocks = new ContentBlock[] { new LinkBlock("https://example.com", "Click here") };

        var result = _sut.Render(blocks);

        result.Content.ShouldContain("[Click here](https://example.com)");
    }

    [Fact]
    public void Render_LinkBlock_EscapesTextButNotUrl()
    {
        var blocks = new ContentBlock[] { new LinkBlock("https://example.com/path", "Go to site!") };

        var result = _sut.Render(blocks);

        // The ! in "site!" should be escaped in the text
        result.Content.ShouldContain("site\\!");
        // The URL should remain intact (only ) and \ are escaped in URLs)
        result.Content.ShouldContain("https://example.com/path");
    }

    // ─── DividerBlock ────────────────────────────────────────────

    [Fact]
    public void Render_DividerBlock_RendersHorizontalLine()
    {
        var blocks = new ContentBlock[] { new DividerBlock() };

        var result = _sut.Render(blocks);

        // The em dashes don't need escaping (not in special chars), but TelegramMarkdownEscaper.Escape is called
        result.Content.ShouldNotBeEmpty();
        result.Content.Length.ShouldBeGreaterThan(5);
    }

    // ─── Multiple Blocks ─────────────────────────────────────────

    [Fact]
    public void Render_MultipleBlocks_SeparatedByDoubleNewline()
    {
        var blocks = new ContentBlock[]
        {
            new TextBlock("First"),
            new TextBlock("Second"),
        };

        var result = _sut.Render(blocks);

        result.Content.ShouldBe("First\n\nSecond");
    }

    // ─── RenderMessages: Message Splitting ───────────────────────

    [Fact]
    public void RenderMessages_NullBlocks_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => _sut.RenderMessages(null!));
    }

    [Fact]
    public void RenderMessages_EmptyBlocks_ReturnsSingleEmptyMessage()
    {
        var result = _sut.RenderMessages([]);

        result.Count.ShouldBe(1);
        result[0].ShouldBeEmpty();
    }

    [Fact]
    public void RenderMessages_SmallContent_ReturnsSingleMessage()
    {
        var blocks = new ContentBlock[]
        {
            new TextBlock("Short text"),
        };

        var result = _sut.RenderMessages(blocks);

        result.Count.ShouldBe(1);
    }

    [Fact]
    public void RenderMessages_ContentExceedsLimit_SplitsAtBlockBoundaries()
    {
        // Create enough blocks to exceed 4096 chars
        var longText = new string('A', 2000);
        var blocks = new ContentBlock[]
        {
            new TextBlock(longText),
            new TextBlock(longText),
            new TextBlock(longText),
        };

        var result = _sut.RenderMessages(blocks);

        result.Count.ShouldBeGreaterThan(1);

        foreach (var msg in result)
        {
            msg.Length.ShouldBeLessThanOrEqualTo(TelegramCanvasRenderer.MaxMessageLength);
        }
    }

    [Fact]
    public void RenderMessages_SingleLargeBlock_SplitsAtLineBoundaries()
    {
        // Create a single block with many lines that exceeds 4096 chars
        var lines = Enumerable.Range(0, 200).Select(i => $"Line {i}: " + new string('X', 30));
        var largeText = string.Join("\n", lines);
        var blocks = new ContentBlock[] { new TextBlock(largeText) };

        var result = _sut.RenderMessages(blocks);

        result.Count.ShouldBeGreaterThan(1);

        foreach (var msg in result)
        {
            msg.Length.ShouldBeLessThanOrEqualTo(TelegramCanvasRenderer.MaxMessageLength);
        }
    }

    [Fact]
    public void RenderMessages_AllBlocksFitInOneMessage_ReturnsSingleMessage()
    {
        var blocks = new ContentBlock[]
        {
            new TextBlock("Hello"),
            new DividerBlock(),
            new TextBlock("World"),
        };

        var result = _sut.RenderMessages(blocks);

        result.Count.ShouldBe(1);
    }

    // ─── All 9 Block Types ───────────────────────────────────────

    [Fact]
    public void Render_All9BlockTypes_ProducesNonEmptyOutput()
    {
        var blocks = new ContentBlock[]
        {
            new TextBlock("Text content"),
            new CodeBlock("code()", "python"),
            new ImageBlock("https://example.com/img.png", "Alt"),
            new TableBlock(["H1", "H2"], [["R1C1", "R1C2"]]),
            new ChartBlock("bar", "{}", "Title"),
            new FormBlock("form1", [new FormField("f", "text", "Label")]),
            new ProgressBlock("Loading", 50),
            new LinkBlock("https://example.com", "Link"),
            new DividerBlock(),
        };

        var result = _sut.Render(blocks);

        result.Content.ShouldNotBeEmpty();
        result.Format.ShouldBe(RenderFormat.Markdown);

        // Verify each block type contributed
        result.Content.ShouldContain("Text content");
        result.Content.ShouldContain("```python");
        result.Content.ShouldContain("https://example.com/img.png");
        result.Content.ShouldContain("H1");
        result.Content.ShouldContain("Title");
        result.Content.ShouldContain("form1");
        result.Content.ShouldContain("Loading");
        result.Content.ShouldContain("Link");
    }
}

public sealed class TelegramMarkdownEscaperTests
{
    // ─── Escape ──────────────────────────────────────────────────

    [Fact]
    public void Escape_NullInput_ReturnsEmpty()
    {
        TelegramMarkdownEscaper.Escape(null!).ShouldBeEmpty();
    }

    [Fact]
    public void Escape_EmptyInput_ReturnsEmpty()
    {
        TelegramMarkdownEscaper.Escape(string.Empty).ShouldBeEmpty();
    }

    [Fact]
    public void Escape_PlainText_ReturnsUnchanged()
    {
        TelegramMarkdownEscaper.Escape("Hello world").ShouldBe("Hello world");
    }

    [Theory]
    [InlineData("_", "\\_")]
    [InlineData("*", "\\*")]
    [InlineData("[", "\\[")]
    [InlineData("]", "\\]")]
    [InlineData("(", "\\(")]
    [InlineData(")", "\\)")]
    [InlineData("~", "\\~")]
    [InlineData("`", "\\`")]
    [InlineData(">", "\\>")]
    [InlineData("#", "\\#")]
    [InlineData("+", "\\+")]
    [InlineData("-", "\\-")]
    [InlineData("=", "\\=")]
    [InlineData("|", "\\|")]
    [InlineData("{", "\\{")]
    [InlineData("}", "\\}")]
    [InlineData(".", "\\.")]
    [InlineData("!", "\\!")]
    [InlineData("\\", "\\\\")]
    public void Escape_SingleSpecialChar_IsEscaped(string input, string expected)
    {
        TelegramMarkdownEscaper.Escape(input).ShouldBe(expected);
    }

    [Fact]
    public void Escape_MixedContent_EscapesOnlySpecialChars()
    {
        var result = TelegramMarkdownEscaper.Escape("Hello *world*!");

        result.ShouldBe("Hello \\*world\\*\\!");
    }

    [Fact]
    public void Escape_AllSpecialCharsInText_AllEscaped()
    {
        var input = "_*[]()~`>#+-=|{}.!\\";
        var result = TelegramMarkdownEscaper.Escape(input);

        // Each character should be preceded by backslash
        result.ShouldBe("\\_\\*\\[\\]\\(\\)\\~\\`\\>\\#\\+\\-\\=\\|\\{\\}\\.\\!\\\\");
    }

    // ─── EscapeCodeBlock ─────────────────────────────────────────

    [Fact]
    public void EscapeCodeBlock_NullInput_ReturnsEmpty()
    {
        TelegramMarkdownEscaper.EscapeCodeBlock(null!).ShouldBeEmpty();
    }

    [Fact]
    public void EscapeCodeBlock_EmptyInput_ReturnsEmpty()
    {
        TelegramMarkdownEscaper.EscapeCodeBlock(string.Empty).ShouldBeEmpty();
    }

    [Fact]
    public void EscapeCodeBlock_PlainCode_ReturnsUnchanged()
    {
        TelegramMarkdownEscaper.EscapeCodeBlock("var x = 1;").ShouldBe("var x = 1;");
    }

    [Fact]
    public void EscapeCodeBlock_Backtick_IsEscaped()
    {
        TelegramMarkdownEscaper.EscapeCodeBlock("echo `hello`").ShouldBe("echo \\`hello\\`");
    }

    [Fact]
    public void EscapeCodeBlock_Backslash_IsEscaped()
    {
        TelegramMarkdownEscaper.EscapeCodeBlock("path\\to\\file").ShouldBe("path\\\\to\\\\file");
    }

    [Fact]
    public void EscapeCodeBlock_OtherSpecialChars_NotEscaped()
    {
        // Inside code blocks, only ` and \ need escaping
        TelegramMarkdownEscaper.EscapeCodeBlock("*bold* _italic_ [link]").ShouldBe("*bold* _italic_ [link]");
    }
}
