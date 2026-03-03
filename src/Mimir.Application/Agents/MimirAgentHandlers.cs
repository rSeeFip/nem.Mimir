using Microsoft.Extensions.Logging;
using nem.Cognitive.Agents.Interfaces;
using nem.Cognitive.Agents.Models;

namespace Mimir.Application.Agents;

/// <summary>
/// Wolverine handlers for the Mimir Reasoner Agent commands.
/// Each handler follows Wolverine's static HandleAsync convention.
/// </summary>
public static class MimirAgentHandlers
{
    /// <summary>
    /// Handles the <see cref="MimirAgentCommands.ReasonAboutQuery"/> command.
    /// Invokes the Reasoner Agent with conversation context and returns a structured response.
    /// </summary>
    public static async Task<MimirAgentCommands.ReasoningResponse> HandleAsync(
        MimirAgentCommands.ReasonAboutQuery command,
        INemAgent agent,
        ConversationMemoryService memoryService,
        ILogger<MimirAgentCommands.ReasonAboutQuery> logger,
        CancellationToken ct)
    {
        logger.LogInformation("Handling ReasonAboutQuery for conversation {ConversationId}",
            command.ConversationId);

        // Build agent context with conversation history from memory
        var history = memoryService.GetConversationHistory(command.ConversationId);
        var context = new AgentContext
        {
            CorrelationId = command.ConversationId,
            UserId = command.UserId,
            ConversationHistory = history,
        };

        var response = await agent.InvokeAsync(command.Query, context, ct).ConfigureAwait(false);

        // Determine intent for metadata
        var router = new QueryRouter();
        var intent = router.AnalyzeIntent(command.Query);

        return new MimirAgentCommands.ReasoningResponse(
            Content: response.Content,
            AgentId: response.AgentId,
            TokensUsed: response.TokensUsed,
            IntentCategory: intent.Category,
            RoutedService: intent.SuggestedService,
            DurationMs: (long)response.Duration.TotalMilliseconds);
    }

    /// <summary>
    /// Handles the <see cref="MimirAgentCommands.RouteToService"/> command.
    /// Routes a query to the appropriate service domain based on intent analysis.
    /// </summary>
    public static Task<RoutingResult> HandleAsync(
        MimirAgentCommands.RouteToService command,
        ILogger<MimirAgentCommands.RouteToService> logger,
        CancellationToken ct)
    {
        logger.LogInformation("Routing query to service for intent category {Category}",
            command.IntentCategory);

        var targetService = QueryRouter.GetServiceForCategory(command.IntentCategory);

        // In this implementation, routing is informational — actual cross-service
        // invocation via Wolverine IMessageBus will be wired in Task 35 (agent-to-agent communication).
        // For now, return the routing decision.
        var result = new RoutingResult(
            ServiceName: targetService,
            Success: true,
            Response: $"Query routed to {targetService} service based on '{command.IntentCategory}' intent.");

        return Task.FromResult(result);
    }

    /// <summary>
    /// Handles the <see cref="MimirAgentCommands.AnalyzeIntent"/> command.
    /// Analyzes user intent and returns the classification result.
    /// </summary>
    public static Task<IntentAnalysis> HandleAsync(
        MimirAgentCommands.AnalyzeIntent command,
        ILogger<MimirAgentCommands.AnalyzeIntent> logger,
        CancellationToken ct)
    {
        logger.LogDebug("Analyzing intent for query: {QueryPreview}",
            command.Query.Length > 100 ? command.Query[..100] + "..." : command.Query);

        var router = new QueryRouter();
        var analysis = router.AnalyzeIntent(command.Query);

        logger.LogInformation("Intent analysis result: Category={Category}, Confidence={Confidence}, Service={Service}",
            analysis.Category, analysis.Confidence, analysis.SuggestedService);

        return Task.FromResult(analysis);
    }
}
