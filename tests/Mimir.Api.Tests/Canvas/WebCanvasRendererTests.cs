using System.Net;
using Mimir.Api.Canvas;
using nem.Contracts.Canvas;
using Shouldly;

namespace Mimir.Api.Tests.Canvas;

public sealed class WebCanvasRendererTests
{
    private readonly WebCanvasRenderer _sut = new();

    // ── Format Property ─────────────────────────────────────────────────────

    [Fact]
    public void Format_ShouldReturn_Html()
    {
        _sut.Format.ShouldBe(RenderFormat.Html);
    }

    // ── Null / Empty Blocks ─────────────────────────────────────────────────

    [Fact]
    public void Render_NullBlocks_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => _sut.Render(null!));
    }

    [Fact]
    public void Render_EmptyBlocks_ReturnsEmptyHtmlContent()
    {
        var result = _sut.Render([]);

        result.Format.ShouldBe(RenderFormat.Html);
        result.Content.ShouldBeEmpty();
    }

    // ── TextBlock ───────────────────────────────────────────────────────────

    [Fact]
    public void Render_TextBlock_ProducesProseDiv()
    {
        var blocks = new ContentBlock[] { new TextBlock("Hello World") };

        var result = _sut.Render(blocks);

        result.Content.ShouldContain("<div class=\"prose\">");
        result.Content.ShouldContain("Hello World");
        result.Content.ShouldContain("</div>");
    }

    [Fact]
    public void Render_TextBlock_HtmlEncodesContent()
    {
        var blocks = new ContentBlock[] { new TextBlock("<script>alert('xss')</script>") };

        var result = _sut.Render(blocks);

        result.Content.ShouldNotContain("<script>");
        result.Content.ShouldContain(WebUtility.HtmlEncode("<script>alert('xss')</script>"));
    }

    // ── CodeBlock ───────────────────────────────────────────────────────────

    [Fact]
    public void Render_CodeBlock_ProducesPreCodeWithLanguageClass()
    {
        var blocks = new ContentBlock[] { new CodeBlock("var x = 1;", "csharp") };

        var result = _sut.Render(blocks);

        result.Content.ShouldContain("<pre");
        result.Content.ShouldContain("<code class=\"language-csharp\">");
        result.Content.ShouldContain("var x = 1;");
        result.Content.ShouldContain("</code>");
        result.Content.ShouldContain("</pre>");
    }

    [Fact]
    public void Render_CodeBlock_WithTitle_IncludesTitle()
    {
        var blocks = new ContentBlock[] { new CodeBlock("print('hi')", "python", "Example") };

        var result = _sut.Render(blocks);

        result.Content.ShouldContain("Example");
    }

    [Fact]
    public void Render_CodeBlock_HtmlEncodesCode()
    {
        var blocks = new ContentBlock[] { new CodeBlock("<div>not html</div>", "html") };

        var result = _sut.Render(blocks);

        result.Content.ShouldContain(WebUtility.HtmlEncode("<div>not html</div>"));
        result.Content.ShouldNotContain("<div>not html</div>");
    }

    [Fact]
    public void Render_CodeBlock_HtmlEncodesLanguage()
    {
        var blocks = new ContentBlock[] { new CodeBlock("x", "c\"sharp") };

        var result = _sut.Render(blocks);

        result.Content.ShouldContain("language-c&quot;sharp");
    }

    // ── ImageBlock ──────────────────────────────────────────────────────────

    [Fact]
    public void Render_ImageBlock_ProducesFigureWithImg()
    {
        var blocks = new ContentBlock[] { new ImageBlock("https://example.com/image.png", "A photo", 800, 600) };

        var result = _sut.Render(blocks);

        result.Content.ShouldContain("<figure");
        result.Content.ShouldContain("<img");
        result.Content.ShouldContain("src=\"https://example.com/image.png\"");
        result.Content.ShouldContain("alt=\"A photo\"");
        result.Content.ShouldContain("width=\"800\"");
        result.Content.ShouldContain("height=\"600\"");
        result.Content.ShouldContain("loading=\"lazy\"");
        result.Content.ShouldContain("</figure>");
    }

    [Fact]
    public void Render_ImageBlock_WithoutDimensions_OmitsWidthHeight()
    {
        var blocks = new ContentBlock[] { new ImageBlock("https://example.com/img.png") };

        var result = _sut.Render(blocks);

        result.Content.ShouldNotContain("width=");
        result.Content.ShouldNotContain("height=");
    }

    [Fact]
    public void Render_ImageBlock_HtmlEncodesAltText()
    {
        var blocks = new ContentBlock[] { new ImageBlock("https://example.com/i.png", "alt\"with<xss>") };

        var result = _sut.Render(blocks);

        result.Content.ShouldContain(WebUtility.HtmlEncode("alt\"with<xss>"));
    }

    [Fact]
    public void Render_ImageBlock_ValidatesUrlScheme_RejectsJavascript()
    {
        // javascript: URLs should be caught by the validator (which throws before rendering)
        // But as defense-in-depth, let's verify the validator catches it
        var blocks = new ContentBlock[] { new ImageBlock("javascript:alert(1)") };

        Should.Throw<ArgumentException>(() => _sut.Render(blocks));
    }

    // ── TableBlock ──────────────────────────────────────────────────────────

    [Fact]
    public void Render_TableBlock_ProducesSemanticTable()
    {
        var blocks = new ContentBlock[]
        {
            new TableBlock(
                ["Name", "Age"],
                [["Alice", "30"], ["Bob", "25"]],
                "People Table")
        };

        var result = _sut.Render(blocks);

        result.Content.ShouldContain("<table");
        result.Content.ShouldContain("<caption");
        result.Content.ShouldContain("People Table");
        result.Content.ShouldContain("<thead");
        result.Content.ShouldContain("<th");
        result.Content.ShouldContain("Name");
        result.Content.ShouldContain("Age");
        result.Content.ShouldContain("<tbody");
        result.Content.ShouldContain("<td");
        result.Content.ShouldContain("Alice");
        result.Content.ShouldContain("30");
        result.Content.ShouldContain("</table>");
    }

    [Fact]
    public void Render_TableBlock_WithoutCaption_OmitsCaption()
    {
        var blocks = new ContentBlock[]
        {
            new TableBlock(["Col1"], [["Val1"]])
        };

        var result = _sut.Render(blocks);

        result.Content.ShouldNotContain("<caption");
    }

    [Fact]
    public void Render_TableBlock_HtmlEncodesCellValues()
    {
        var blocks = new ContentBlock[]
        {
            new TableBlock(
                ["Header<script>"],
                [["<b>bold</b>"]])
        };

        var result = _sut.Render(blocks);

        result.Content.ShouldContain(WebUtility.HtmlEncode("Header<script>"));
        result.Content.ShouldContain(WebUtility.HtmlEncode("<b>bold</b>"));
    }

    // ── ChartBlock ──────────────────────────────────────────────────────────

    [Fact]
    public void Render_ChartBlock_ProducesDataAttributes_NoJavaScript()
    {
        var dataJson = """{"labels":["A","B"],"values":[1,2]}""";
        var blocks = new ContentBlock[] { new ChartBlock("bar", dataJson, "Sales") };

        var result = _sut.Render(blocks);

        result.Content.ShouldContain("data-chart-type=\"bar\"");
        result.Content.ShouldContain("data-chart-data=");
        result.Content.ShouldContain("Sales");
        result.Content.ShouldNotContain("<script");
        result.Content.ShouldNotContain("javascript:");
    }

    [Fact]
    public void Render_ChartBlock_HtmlEncodesDataJson()
    {
        var dataJson = """{"key":"<script>alert('xss')</script>"}""";
        var blocks = new ContentBlock[] { new ChartBlock("line", dataJson) };

        var result = _sut.Render(blocks);

        result.Content.ShouldNotContain("<script>alert");
    }

    // ── FormBlock ───────────────────────────────────────────────────────────

    [Fact]
    public void Render_FormBlock_ProducesDisabledReadonlyForm()
    {
        var fields = new[]
        {
            new FormField("name", "text", "Your Name", true, "Default"),
            new FormField("email", "email", "Email Address"),
        };
        var blocks = new ContentBlock[] { new FormBlock("contact-form", fields) };

        var result = _sut.Render(blocks);

        result.Content.ShouldContain("<form");
        result.Content.ShouldContain("disabled");
        result.Content.ShouldContain("readonly");
        result.Content.ShouldContain("Your Name");
        result.Content.ShouldContain("Email Address");
        result.Content.ShouldNotContain("action=");
        result.Content.ShouldNotContain("method=");
    }

    [Fact]
    public void Render_FormBlock_HtmlEncodesFieldLabels()
    {
        var fields = new[] { new FormField("f", "text", "<script>label</script>") };
        var blocks = new ContentBlock[] { new FormBlock("form1", fields) };

        var result = _sut.Render(blocks);

        result.Content.ShouldContain(WebUtility.HtmlEncode("<script>label</script>"));
        result.Content.ShouldNotContain("<script>label</script>");
    }

    [Fact]
    public void Render_FormBlock_IncludesDefaultValue()
    {
        var fields = new[] { new FormField("city", "text", "City", false, "Stockholm") };
        var blocks = new ContentBlock[] { new FormBlock("form2", fields) };

        var result = _sut.Render(blocks);

        result.Content.ShouldContain("Stockholm");
    }

    // ── ProgressBlock ───────────────────────────────────────────────────────

    [Fact]
    public void Render_ProgressBlock_ProducesProgressElement()
    {
        var blocks = new ContentBlock[] { new ProgressBlock("Loading", 75.5, "In Progress") };

        var result = _sut.Render(blocks);

        result.Content.ShouldContain("<progress");
        result.Content.ShouldContain("value=\"75.5\"");
        result.Content.ShouldContain("max=\"100\"");
        result.Content.ShouldContain("Loading");
    }

    [Fact]
    public void Render_ProgressBlock_WithStatus_IncludesStatus()
    {
        var blocks = new ContentBlock[] { new ProgressBlock("Upload", 50, "Uploading...") };

        var result = _sut.Render(blocks);

        result.Content.ShouldContain("Uploading...");
    }

    [Fact]
    public void Render_ProgressBlock_HtmlEncodesLabel()
    {
        var blocks = new ContentBlock[] { new ProgressBlock("<b>Task</b>", 10) };

        var result = _sut.Render(blocks);

        result.Content.ShouldContain(WebUtility.HtmlEncode("<b>Task</b>"));
    }

    // ── LinkBlock ───────────────────────────────────────────────────────────

    [Fact]
    public void Render_LinkBlock_ProducesAnchorWithSecurityAttrs()
    {
        var blocks = new ContentBlock[] { new LinkBlock("https://example.com", "Example Site", "Visit") };

        var result = _sut.Render(blocks);

        result.Content.ShouldContain("<a");
        result.Content.ShouldContain("href=\"https://example.com\"");
        result.Content.ShouldContain("rel=\"noopener noreferrer\"");
        result.Content.ShouldContain("target=\"_blank\"");
        result.Content.ShouldContain("Example Site");
        result.Content.ShouldContain("</a>");
    }

    [Fact]
    public void Render_LinkBlock_HtmlEncodesText()
    {
        var blocks = new ContentBlock[] { new LinkBlock("https://example.com", "<b>Click</b>") };

        var result = _sut.Render(blocks);

        result.Content.ShouldContain(WebUtility.HtmlEncode("<b>Click</b>"));
    }

    [Fact]
    public void Render_LinkBlock_JavascriptUrl_ThrowsViaValidator()
    {
        var blocks = new ContentBlock[] { new LinkBlock("javascript:alert(1)", "Hack") };

        Should.Throw<ArgumentException>(() => _sut.Render(blocks));
    }

    [Fact]
    public void Render_LinkBlock_WithTitle_IncludesTitleAttr()
    {
        var blocks = new ContentBlock[] { new LinkBlock("https://ex.com", "Link", "My Title") };

        var result = _sut.Render(blocks);

        result.Content.ShouldContain("title=\"My Title\"");
    }

    // ── DividerBlock ────────────────────────────────────────────────────────

    [Fact]
    public void Render_DividerBlock_ProducesHr()
    {
        var blocks = new ContentBlock[] { new DividerBlock() };

        var result = _sut.Render(blocks);

        result.Content.ShouldContain("<hr");
    }

    // ── Multiple Blocks ─────────────────────────────────────────────────────

    [Fact]
    public void Render_MultipleBlocks_RendersAllInOrder()
    {
        var blocks = new ContentBlock[]
        {
            new TextBlock("First"),
            new DividerBlock(),
            new TextBlock("Second"),
        };

        var result = _sut.Render(blocks);

        var firstIndex = result.Content.IndexOf("First", StringComparison.Ordinal);
        var hrIndex = result.Content.IndexOf("<hr", StringComparison.Ordinal);
        var secondIndex = result.Content.IndexOf("Second", StringComparison.Ordinal);

        firstIndex.ShouldBeLessThan(hrIndex);
        hrIndex.ShouldBeLessThan(secondIndex);
    }

    // ── Validation ──────────────────────────────────────────────────────────

    [Fact]
    public void Render_InvalidBlock_ThrowsArgumentException()
    {
        // TextBlock with empty text is invalid per ContentBlockValidator
        var blocks = new ContentBlock[] { new TextBlock("") };

        Should.Throw<ArgumentException>(() => _sut.Render(blocks));
    }

    [Fact]
    public void Render_MixedValidAndInvalidBlocks_ThrowsArgumentException()
    {
        var blocks = new ContentBlock[]
        {
            new TextBlock("Valid text"),
            new CodeBlock("", ""), // invalid
        };

        Should.Throw<ArgumentException>(() => _sut.Render(blocks));
    }

    // ── XSS Prevention Comprehensive ────────────────────────────────────────

    [Theory]
    [InlineData("<script>alert('xss')</script>")]
    [InlineData("<img onerror=\"alert(1)\" src=\"x\">")]
    [InlineData("\" onmouseover=\"alert(1)")]
    public void Render_TextBlock_XssPayloads_AreHtmlEncoded(string xssPayload)
    {
        var blocks = new ContentBlock[] { new TextBlock(xssPayload) };

        var result = _sut.Render(blocks);

        // The raw XSS payload should NOT appear unencoded
        result.Content.ShouldNotContain(xssPayload);
        // The encoded version should be present
        result.Content.ShouldContain(WebUtility.HtmlEncode(xssPayload));
    }

    [Fact]
    public void Render_ImageBlock_JavascriptProtocol_CaseInsensitive_Throws()
    {
        var blocks = new ContentBlock[] { new ImageBlock("JAVASCRIPT:alert(1)") };

        Should.Throw<ArgumentException>(() => _sut.Render(blocks));
    }

    // ── RenderedOutput Format ───────────────────────────────────────────────

    [Fact]
    public void Render_AlwaysReturns_HtmlFormat()
    {
        var blocks = new ContentBlock[] { new TextBlock("test") };

        var result = _sut.Render(blocks);

        result.Format.ShouldBe(RenderFormat.Html);
    }

    // ── All 9 Block Types Together ──────────────────────────────────────────

    [Fact]
    public void Render_All9BlockTypes_ProducesValidHtml()
    {
        var blocks = new ContentBlock[]
        {
            new TextBlock("Hello"),
            new CodeBlock("code", "js"),
            new ImageBlock("https://img.example.com/pic.jpg", "photo"),
            new TableBlock(["H1"], [["V1"]]),
            new ChartBlock("bar", """{"data":[1]}"""),
            new FormBlock("f1", [new FormField("name", "text", "Name")]),
            new ProgressBlock("Loading", 50),
            new LinkBlock("https://link.example.com", "Click here"),
            new DividerBlock(),
        };

        var result = _sut.Render(blocks);

        result.Format.ShouldBe(RenderFormat.Html);
        result.Content.ShouldNotBeNullOrWhiteSpace();

        // Each block type has its characteristic HTML
        result.Content.ShouldContain("<div class=\"prose\">");
        result.Content.ShouldContain("<pre");
        result.Content.ShouldContain("<figure");
        result.Content.ShouldContain("<table");
        result.Content.ShouldContain("data-chart-type=");
        result.Content.ShouldContain("<form");
        result.Content.ShouldContain("<progress");
        result.Content.ShouldContain("<a ");
        result.Content.ShouldContain("<hr");
    }
}
