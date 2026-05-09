using Shouldly;
using nem.Mimir.McpServer.Prompts;

namespace nem.Mimir.McpServer.Tests.Prompts;

public sealed class MimirCodeReviewPromptTests
{
    [Fact]
    public void GetPrompt_ReturnsSystemAndUserMessages()
    {
        var messages = MimirCodeReviewPrompt.GetPrompt("csharp", "var x = 1;", null).ToList();

        messages.Count.ShouldBe(2);
        messages[0].Role.Value.ShouldBe("system");
        messages[1].Role.Value.ShouldBe("user");
    }

    [Fact]
    public void GetPrompt_UserMessageContainsCodeAndLanguage()
    {
        var code = "public void Foo() { }";
        var messages = MimirCodeReviewPrompt.GetPrompt("csharp", code, "security").ToList();

        var content = messages[1].Text;
        content.ShouldNotBeNull();
        content.ShouldContain("csharp");
        content.ShouldContain(code);
        content.ShouldContain("security");
    }

    [Fact]
    public void GetPrompt_WithNullFocusAreas_UsesComprehensiveReview()
    {
        var messages = MimirCodeReviewPrompt.GetPrompt("python", "print('hi')", null).ToList();

        var content = messages[1].Text;
        content.ShouldNotBeNull();
        content.ShouldContain("comprehensive review");
    }
}
