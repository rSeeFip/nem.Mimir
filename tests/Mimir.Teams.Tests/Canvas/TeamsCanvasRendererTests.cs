namespace Mimir.Teams.Tests.Canvas;

using System.Text.Json;
using nem.Contracts.Canvas;
using Mimir.Teams.Canvas;
using Shouldly;

public sealed class TeamsCanvasRendererTests
{
    private readonly TeamsCanvasRenderer _sut = new();

    #region Interface / Format

    [Fact]
    public void Format_ShouldBe_AdaptiveCard()
    {
        _sut.Format.ShouldBe(RenderFormat.AdaptiveCard);
    }

    [Fact]
    public void Render_ShouldImplement_ICanvasRenderer()
    {
        ICanvasRenderer renderer = _sut;
        renderer.ShouldNotBeNull();
        renderer.Format.ShouldBe(RenderFormat.AdaptiveCard);
    }

    [Fact]
    public void Render_ReturnsRenderedOutput_WithAdaptiveCardFormat()
    {
        var blocks = new List<ContentBlock> { new TextBlock("Hello") };

        var result = _sut.Render(blocks);

        result.ShouldNotBeNull();
        result.Format.ShouldBe(RenderFormat.AdaptiveCard);
        result.Content.ShouldNotBeNullOrWhiteSpace();
    }

    #endregion

    #region Card Envelope

    [Fact]
    public void Render_ProducesValidAdaptiveCardEnvelope()
    {
        var blocks = new List<ContentBlock> { new TextBlock("Test") };

        var result = _sut.Render(blocks);
        var doc = JsonDocument.Parse(result.Content);
        var root = doc.RootElement;

        root.GetProperty("type").GetString().ShouldBe("AdaptiveCard");
        root.GetProperty("version").GetString().ShouldBe("1.5");
        root.GetProperty("$schema").GetString()!.ShouldContain("adaptivecards.io");
        root.GetProperty("body").ValueKind.ShouldBe(JsonValueKind.Array);
    }

    #endregion

    #region Empty / Null

    [Fact]
    public void Render_EmptyList_ReturnsCardWithEmptyBody()
    {
        var blocks = new List<ContentBlock>();

        var result = _sut.Render(blocks);
        var doc = JsonDocument.Parse(result.Content);

        doc.RootElement.GetProperty("body").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public void Render_NullBlocks_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => _sut.Render(null!));
    }

    #endregion

    #region TextBlock

    [Fact]
    public void Render_TextBlock_RendersAsTextBlockWithWrap()
    {
        var blocks = new List<ContentBlock> { new TextBlock("Hello Teams!") };

        var element = GetFirstBodyElement(blocks);

        element.GetProperty("type").GetString().ShouldBe("TextBlock");
        element.GetProperty("text").GetString().ShouldBe("Hello Teams!");
        element.GetProperty("wrap").GetBoolean().ShouldBeTrue();
    }

    #endregion

    #region CodeBlock

    [Fact]
    public void Render_CodeBlock_RendersAsMonospaceTextBlock()
    {
        var blocks = new List<ContentBlock> { new CodeBlock("var x = 1;", "csharp") };

        var element = GetFirstBodyElement(blocks);

        element.GetProperty("type").GetString().ShouldBe("TextBlock");
        element.GetProperty("text").GetString().ShouldBe("var x = 1;");
        element.GetProperty("fontType").GetString().ShouldBe("Monospace");
        element.GetProperty("wrap").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public void Render_CodeBlock_WithTitle_HasSeparator()
    {
        var blocks = new List<ContentBlock> { new CodeBlock("code", "js", "Example") };

        var element = GetFirstBodyElement(blocks);

        element.GetProperty("separator").GetBoolean().ShouldBeTrue();
    }

    #endregion

    #region ImageBlock

    [Fact]
    public void Render_ImageBlock_RendersAsImageElement()
    {
        var blocks = new List<ContentBlock> { new ImageBlock("https://example.com/img.png", "A picture") };

        var element = GetFirstBodyElement(blocks);

        element.GetProperty("type").GetString().ShouldBe("Image");
        element.GetProperty("url").GetString().ShouldBe("https://example.com/img.png");
        element.GetProperty("altText").GetString().ShouldBe("A picture");
    }

    [Fact]
    public void Render_ImageBlock_WithDimensions_SetsSizeAuto()
    {
        var blocks = new List<ContentBlock> { new ImageBlock("https://example.com/img.png", Width: 200) };

        var element = GetFirstBodyElement(blocks);

        element.GetProperty("size").GetString().ShouldBe("Auto");
    }

    [Fact]
    public void Render_ImageBlock_WithoutAltText_OmitsAltText()
    {
        var blocks = new List<ContentBlock> { new ImageBlock("https://example.com/img.png") };

        var element = GetFirstBodyElement(blocks);

        element.TryGetProperty("altText", out _).ShouldBeFalse();
    }

    #endregion

    #region TableBlock

    [Fact]
    public void Render_TableBlock_RendersAsTableElement()
    {
        var blocks = new List<ContentBlock>
        {
            new TableBlock(["Name", "Age"], [["Alice", "30"], ["Bob", "25"]])
        };

        var element = GetFirstBodyElement(blocks);

        element.GetProperty("type").GetString().ShouldBe("Table");
        element.GetProperty("columns").GetArrayLength().ShouldBe(2);
        element.GetProperty("rows").GetArrayLength().ShouldBe(3); // 1 header + 2 data rows
    }

    [Fact]
    public void Render_TableBlock_HeaderRow_HasBolderWeight()
    {
        var blocks = new List<ContentBlock>
        {
            new TableBlock(["Col"], [["Val"]])
        };

        var element = GetFirstBodyElement(blocks);
        var headerRow = element.GetProperty("rows")[0];
        var firstCell = headerRow.GetProperty("cells")[0];
        var textBlock = firstCell.GetProperty("items")[0];

        textBlock.GetProperty("weight").GetString().ShouldBe("Bolder");
    }

    #endregion

    #region ChartBlock

    [Fact]
    public void Render_ChartBlock_RendersAsDescriptiveText()
    {
        var blocks = new List<ContentBlock>
        {
            new ChartBlock("bar", "{\"data\":[1,2,3]}", "Sales Report")
        };

        var element = GetFirstBodyElement(blocks);

        element.GetProperty("type").GetString().ShouldBe("TextBlock");
        var text = element.GetProperty("text").GetString()!;
        text.ShouldContain("Chart");
        text.ShouldContain("Sales Report");
        text.ShouldContain("bar");
    }

    [Fact]
    public void Render_ChartBlock_WithoutTitle_ContainsChartType()
    {
        var blocks = new List<ContentBlock>
        {
            new ChartBlock("pie", "{\"data\":[1]}")
        };

        var element = GetFirstBodyElement(blocks);
        var text = element.GetProperty("text").GetString()!;

        text.ShouldContain("pie");
    }

    #endregion

    #region FormBlock

    [Fact]
    public void Render_FormBlock_RendersAsContainerWithInputs()
    {
        var fields = new[]
        {
            new FormField("username", "text", "Username", Required: true),
            new FormField("age", "number", "Age"),
        };
        var blocks = new List<ContentBlock> { new FormBlock("form1", fields) };

        var element = GetFirstBodyElement(blocks);

        element.GetProperty("type").GetString().ShouldBe("Container");
        var items = element.GetProperty("items");
        items.GetArrayLength().ShouldBe(2);
    }

    [Fact]
    public void Render_FormBlock_TextFieldRendersAsInputText()
    {
        var fields = new[] { new FormField("name", "text", "Name", Required: true, DefaultValue: "John") };
        var blocks = new List<ContentBlock> { new FormBlock("f1", fields) };

        var element = GetFirstBodyElement(blocks);
        var input = element.GetProperty("items")[0];

        input.GetProperty("type").GetString().ShouldBe("Input.Text");
        input.GetProperty("id").GetString().ShouldBe("name");
        input.GetProperty("label").GetString().ShouldBe("Name");
        input.GetProperty("isRequired").GetBoolean().ShouldBeTrue();
        input.GetProperty("isVisible").GetBoolean().ShouldBeTrue();
        input.GetProperty("value").GetString().ShouldBe("John");
    }

    [Fact]
    public void Render_FormBlock_NumberFieldRendersAsInputNumber()
    {
        var fields = new[] { new FormField("qty", "number", "Quantity") };
        var blocks = new List<ContentBlock> { new FormBlock("f1", fields) };

        var input = GetFirstBodyElement(blocks).GetProperty("items")[0];

        input.GetProperty("type").GetString().ShouldBe("Input.Number");
    }

    [Fact]
    public void Render_FormBlock_DateFieldRendersAsInputDate()
    {
        var fields = new[] { new FormField("dob", "date", "Date of Birth") };
        var blocks = new List<ContentBlock> { new FormBlock("f1", fields) };

        var input = GetFirstBodyElement(blocks).GetProperty("items")[0];

        input.GetProperty("type").GetString().ShouldBe("Input.Date");
    }

    [Fact]
    public void Render_FormBlock_ToggleFieldRendersAsInputToggle()
    {
        var fields = new[] { new FormField("agree", "toggle", "I agree") };
        var blocks = new List<ContentBlock> { new FormBlock("f1", fields) };

        var input = GetFirstBodyElement(blocks).GetProperty("items")[0];

        input.GetProperty("type").GetString().ShouldBe("Input.Toggle");
    }

    [Fact]
    public void Render_FormBlock_ChoiceFieldRendersAsInputChoiceSet()
    {
        var fields = new[] { new FormField("color", "choice", "Favorite Color") };
        var blocks = new List<ContentBlock> { new FormBlock("f1", fields) };

        var input = GetFirstBodyElement(blocks).GetProperty("items")[0];

        input.GetProperty("type").GetString().ShouldBe("Input.ChoiceSet");
    }

    [Fact]
    public void Render_FormBlock_DoesNotContainActionSubmit()
    {
        var fields = new[] { new FormField("x", "text", "X") };
        var blocks = new List<ContentBlock> { new FormBlock("f1", fields) };

        var result = _sut.Render(blocks);

        // G14: No Action.Submit in adaptive card
        result.Content.ShouldNotContain("Action.Submit");
    }

    #endregion

    #region ProgressBlock

    [Fact]
    public void Render_ProgressBlock_RendersAsAsciiProgressBar()
    {
        var blocks = new List<ContentBlock> { new ProgressBlock("Upload", 75.0) };

        var element = GetFirstBodyElement(blocks);

        element.GetProperty("type").GetString().ShouldBe("TextBlock");
        var text = element.GetProperty("text").GetString()!;
        text.ShouldContain("75%");
        text.ShouldContain("Upload");
        text.ShouldContain("█"); // filled portion
        text.ShouldContain("░"); // empty portion
    }

    [Fact]
    public void Render_ProgressBlock_WithStatus_IncludesStatus()
    {
        var blocks = new List<ContentBlock> { new ProgressBlock("Download", 50.0, "In Progress") };

        var element = GetFirstBodyElement(blocks);
        var text = element.GetProperty("text").GetString()!;

        text.ShouldContain("In Progress");
    }

    [Fact]
    public void Render_ProgressBlock_0Percent_AllEmpty()
    {
        var blocks = new List<ContentBlock> { new ProgressBlock("Start", 0.0) };

        var element = GetFirstBodyElement(blocks);
        var text = element.GetProperty("text").GetString()!;

        text.ShouldContain("0%");
    }

    [Fact]
    public void Render_ProgressBlock_100Percent_AllFilled()
    {
        var blocks = new List<ContentBlock> { new ProgressBlock("Done", 100.0) };

        var element = GetFirstBodyElement(blocks);
        var text = element.GetProperty("text").GetString()!;

        text.ShouldContain("100%");
    }

    [Fact]
    public void Render_ProgressBlock_UsesMonospaceFont()
    {
        var blocks = new List<ContentBlock> { new ProgressBlock("Task", 50.0) };

        var element = GetFirstBodyElement(blocks);
        element.GetProperty("fontType").GetString().ShouldBe("Monospace");
    }

    #endregion

    #region LinkBlock

    [Fact]
    public void Render_LinkBlock_RendersAsMarkdownLink()
    {
        var blocks = new List<ContentBlock> { new LinkBlock("https://example.com", "Example") };

        var element = GetFirstBodyElement(blocks);

        element.GetProperty("type").GetString().ShouldBe("TextBlock");
        element.GetProperty("text").GetString().ShouldBe("[Example](https://example.com)");
    }

    [Fact]
    public void Render_LinkBlock_WithTitle_IncludesTitleInLink()
    {
        var blocks = new List<ContentBlock> { new LinkBlock("https://example.com", "Example", "More info") };

        var element = GetFirstBodyElement(blocks);
        var text = element.GetProperty("text").GetString()!;

        text.ShouldContain("\"More info\"");
    }

    #endregion

    #region DividerBlock

    [Fact]
    public void Render_DividerBlock_RendersAsColumnSetWithSeparator()
    {
        var blocks = new List<ContentBlock> { new DividerBlock() };

        var element = GetFirstBodyElement(blocks);

        element.GetProperty("type").GetString().ShouldBe("ColumnSet");
        element.GetProperty("separator").GetBoolean().ShouldBeTrue();
    }

    #endregion

    #region Multiple Blocks

    [Fact]
    public void Render_MultipleBlocks_RendersAllInOrder()
    {
        var blocks = new List<ContentBlock>
        {
            new TextBlock("Title"),
            new DividerBlock(),
            new TextBlock("Content"),
        };

        var result = _sut.Render(blocks);
        var doc = JsonDocument.Parse(result.Content);
        var body = doc.RootElement.GetProperty("body");

        body.GetArrayLength().ShouldBe(3);
        body[0].GetProperty("type").GetString().ShouldBe("TextBlock");
        body[1].GetProperty("type").GetString().ShouldBe("ColumnSet");
        body[2].GetProperty("type").GetString().ShouldBe("TextBlock");
    }

    #endregion

    #region Validation

    [Fact]
    public void Render_InvalidBlocks_ThrowsArgumentException()
    {
        // Empty text fails validation
        var blocks = new List<ContentBlock> { new TextBlock("") };

        var ex = Should.Throw<ArgumentException>(() => _sut.Render(blocks));
        ex.Message.ShouldContain("validation");
    }

    [Fact]
    public void Render_InvalidCodeBlock_ThrowsArgumentException()
    {
        var blocks = new List<ContentBlock> { new CodeBlock("", "") };

        Should.Throw<ArgumentException>(() => _sut.Render(blocks));
    }

    [Fact]
    public void Render_JavascriptUrlInImageBlock_ThrowsArgumentException()
    {
        var blocks = new List<ContentBlock> { new ImageBlock("javascript:alert(1)") };

        var ex = Should.Throw<ArgumentException>(() => _sut.Render(blocks));
        ex.Message.ShouldContain("javascript:");
    }

    [Fact]
    public void Render_JavascriptUrlInLinkBlock_ThrowsArgumentException()
    {
        var blocks = new List<ContentBlock> { new LinkBlock("javascript:void(0)", "Click me") };

        var ex = Should.Throw<ArgumentException>(() => _sut.Render(blocks));
        ex.Message.ShouldContain("javascript:");
    }

    #endregion

    #region 28KB Payload Limit

    [Fact]
    public void Render_LargePayload_TruncatesToLimit()
    {
        // Generate blocks that exceed 28KB
        var blocks = new List<ContentBlock>();
        var largeText = new string('A', 2000);

        for (int i = 0; i < 20; i++)
        {
            blocks.Add(new TextBlock(largeText));
        }

        var result = _sut.Render(blocks);

        System.Text.Encoding.UTF8.GetByteCount(result.Content).ShouldBeLessThanOrEqualTo(TeamsCanvasRenderer.MaxPayloadBytes);
        result.Content.ShouldContain("[Content truncated");
    }

    [Fact]
    public void Render_ExactlyUnderLimit_DoesNotTruncate()
    {
        // Small payload should not be truncated
        var blocks = new List<ContentBlock> { new TextBlock("Small text") };

        var result = _sut.Render(blocks);

        result.Content.ShouldNotContain("[Content truncated");
    }

    #endregion

    #region Helpers

    private JsonElement GetFirstBodyElement(IReadOnlyList<ContentBlock> blocks)
    {
        var result = _sut.Render(blocks);
        var doc = JsonDocument.Parse(result.Content);
        return doc.RootElement.GetProperty("body")[0];
    }

    #endregion
}
