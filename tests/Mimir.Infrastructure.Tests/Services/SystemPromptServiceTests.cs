using Mimir.Infrastructure.Services;
using Shouldly;

namespace Mimir.Infrastructure.Tests.Services;

public sealed class SystemPromptServiceTests
{
    private readonly SystemPromptService _service = new();

    [Fact]
    public void RenderTemplate_ReplacesVariables()
    {
        var template = "Hello {{name}}, welcome to {{app}}!";
        var variables = new Dictionary<string, string>
        {
            ["name"] = "Alice",
            ["app"] = "Mimir"
        };

        var result = _service.RenderTemplate(template, variables);

        result.ShouldBe("Hello Alice, welcome to Mimir!");
    }

    [Fact]
    public void RenderTemplate_PreservesUnmatchedVariables()
    {
        var template = "Hello {{name}}, your role is {{role}}";
        var variables = new Dictionary<string, string>
        {
            ["name"] = "Bob"
        };

        var result = _service.RenderTemplate(template, variables);

        result.ShouldBe("Hello Bob, your role is {{role}}");
    }

    [Fact]
    public void RenderTemplate_HandlesWhitespaceInBraces()
    {
        var template = "Hello {{ name }}, welcome!";
        var variables = new Dictionary<string, string>
        {
            ["name"] = "Charlie"
        };

        var result = _service.RenderTemplate(template, variables);

        result.ShouldBe("Hello Charlie, welcome!");
    }

    [Fact]
    public void RenderTemplate_EmptyTemplate_ReturnsEmpty()
    {
        var result = _service.RenderTemplate(string.Empty, new Dictionary<string, string>());

        result.ShouldBe(string.Empty);
    }

    [Fact]
    public void RenderTemplate_NullTemplate_ReturnsEmpty()
    {
        var result = _service.RenderTemplate(null!, new Dictionary<string, string>());

        result.ShouldBe(string.Empty);
    }

    [Fact]
    public void RenderTemplate_NullVariables_ReturnsTemplate()
    {
        var template = "Hello {{name}}!";

        var result = _service.RenderTemplate(template, null!);

        result.ShouldBe("Hello {{name}}!");
    }

    [Fact]
    public void RenderTemplate_EmptyVariables_ReturnsTemplate()
    {
        var template = "Hello {{name}}!";

        var result = _service.RenderTemplate(template, new Dictionary<string, string>());

        result.ShouldBe("Hello {{name}}!");
    }

    [Fact]
    public void RenderTemplate_NoVariablesInTemplate_ReturnsAsIs()
    {
        var template = "Just plain text without variables.";
        var variables = new Dictionary<string, string>
        {
            ["name"] = "Unused"
        };

        var result = _service.RenderTemplate(template, variables);

        result.ShouldBe("Just plain text without variables.");
    }

    [Fact]
    public void RenderTemplate_MultipleOccurrencesOfSameVariable()
    {
        var template = "{{name}} said hello to {{name}}";
        var variables = new Dictionary<string, string>
        {
            ["name"] = "Alice"
        };

        var result = _service.RenderTemplate(template, variables);

        result.ShouldBe("Alice said hello to Alice");
    }

    [Fact]
    public void RenderTemplate_VariableValueContainsBraces_DoesNotDoubleProcess()
    {
        var template = "Result: {{value}}";
        var variables = new Dictionary<string, string>
        {
            ["value"] = "{{nested}}"
        };

        var result = _service.RenderTemplate(template, variables);

        result.ShouldBe("Result: {{nested}}");
    }

    [Fact]
    public void RenderTemplate_EmptyVariableValue_ReplacesWithEmpty()
    {
        var template = "Hello {{name}}!";
        var variables = new Dictionary<string, string>
        {
            ["name"] = ""
        };

        var result = _service.RenderTemplate(template, variables);

        result.ShouldBe("Hello !");
    }
}
