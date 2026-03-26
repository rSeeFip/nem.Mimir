using System.ComponentModel;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Server;

namespace nem.Mimir.McpServer.Prompts;

/// <summary>
/// MCP prompt that generates a code review prompt for Mimir conversations.
/// </summary>
[McpServerPromptType]
public static class MimirCodeReviewPrompt
{
    [McpServerPrompt(Name = "mimir_code_review"), Description("Generate a code review prompt for analyzing code within a Mimir conversation.")]
    public static IEnumerable<ChatMessage> GetPrompt(
        [Description("The programming language of the code to review.")] string language,
        [Description("The code to review.")] string code,
        [Description("Optional focus areas for the review (e.g. 'security', 'performance').")] string? focusAreas)
    {
        var systemMessage = """
            You are an expert code reviewer with deep knowledge across multiple languages and frameworks.
            Provide thorough, actionable code review feedback. Focus on correctness, readability,
            maintainability, performance, and security. Be specific with line references and suggestions.
            """;

        yield return new ChatMessage(ChatRole.System, systemMessage);

        var userContent = $"""
            Please review the following {language} code:

            ```{language}
            {code}
            ```

            {(focusAreas is not null ? $"Focus areas: {focusAreas}" : "Provide a comprehensive review covering all aspects.")}

            For each issue found, provide:
            1. Severity (Critical/Warning/Suggestion)
            2. Description of the issue
            3. Recommended fix with code example
            """;

        yield return new ChatMessage(ChatRole.User, userContent);
    }
}
