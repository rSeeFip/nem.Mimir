namespace nem.Mimir.Application.Agents;

/// <summary>
/// Wolverine commands for the Mimir Reasoner Agent.
/// These are sealed records following Wolverine's command pattern with static HandleAsync methods.
/// </summary>
public static class MimirAgentCommands
{
    /// <summary>
    /// Command to invoke the Reasoner Agent for a complex multi-domain query.
    /// </summary>
    /// <param name="Query">The user's natural language query.</param>
    /// <param name="ConversationId">The conversation session identifier.</param>
    /// <param name="UserId">Optional user identifier for the session.</param>
    public sealed record ReasonAboutQuery(
        string Query,
        string ConversationId,
        string? UserId = null);

    /// <summary>
    /// The response from a reasoning invocation.
    /// </summary>
    /// <param name="Content">The generated response content.</param>
    /// <param name="AgentId">The agent identifier that generated the response.</param>
    /// <param name="TokensUsed">Total tokens consumed.</param>
    /// <param name="IntentCategory">The detected intent category.</param>
    /// <param name="RoutedService">The service the query was routed to.</param>
    /// <param name="DurationMs">Total wall-clock time in milliseconds.</param>
    public sealed record ReasoningResponse(
        string Content,
        string AgentId,
        int TokensUsed,
        string IntentCategory,
        string RoutedService,
        long DurationMs);

    /// <summary>
    /// Command to route a query to a specific service based on intent classification.
    /// </summary>
    /// <param name="Query">The user's natural language query.</param>
    /// <param name="IntentCategory">The intent category determined by analysis.</param>
    public sealed record RouteToService(
        string Query,
        string IntentCategory);

    /// <summary>
    /// Command to analyze the intent of a user query.
    /// </summary>
    /// <param name="Query">The user's natural language query.</param>
    public sealed record AnalyzeIntent(string Query);
}
