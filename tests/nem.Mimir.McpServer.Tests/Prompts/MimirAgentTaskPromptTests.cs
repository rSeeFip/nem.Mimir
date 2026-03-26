using Shouldly;
using nem.Mimir.McpServer.Prompts;

namespace nem.Mimir.McpServer.Tests.Prompts;

public sealed class MimirAgentTaskPromptTests
{
    [Fact]
    public void GetPrompt_ReturnsSystemAndUserMessages()
    {
        var messages = MimirAgentTaskPrompt.GetPrompt("Analyze", "Review architecture", null).ToList();

        messages.Count.ShouldBe(2);
        messages[0].Role.Value.ShouldBe("system");
        messages[1].Role.Value.ShouldBe("user");
    }

    [Fact]
    public void GetPrompt_UserMessageContainsTaskDetails()
    {
        var messages = MimirAgentTaskPrompt.GetPrompt("Execute", "Run migration script", "Production DB").ToList();

        var userMessage = messages[1];
        var content = userMessage.Text;
        content.ShouldNotBeNull();
        content.ShouldContain("Execute");
        content.ShouldContain("Run migration script");
        content.ShouldContain("Production DB");
    }

    [Fact]
    public void GetPrompt_WithNullContext_OmitsContextLine()
    {
        var messages = MimirAgentTaskPrompt.GetPrompt("Explore", "Find patterns", null).ToList();

        var content = messages[1].Text;
        content.ShouldNotBeNull();
        content.ShouldContain("Explore");
        content.ShouldContain("Find patterns");
    }
}
