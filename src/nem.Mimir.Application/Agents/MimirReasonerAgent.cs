using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using nem.KnowHub.Abstractions.Interfaces;
using nem.KnowHub.Abstractions.Models;
using nem.KnowHub.Agents.Interfaces;
using nem.KnowHub.Agents.Models;

namespace nem.Mimir.Application.Agents;

/// <summary>
/// The Mimir "Prefrontal Cortex" Reasoner Agent — implements <see cref="INemAgent"/> and wraps
/// the existing Mimir chat pipeline via <see cref="ICognitiveClient"/>.
/// </summary>
/// <remarks>
/// This agent is an ADDITIONAL path alongside the existing <c>ILlmService</c> pipeline.
/// It does NOT replace or modify the existing LiteLlm integration.
/// It adds cross-service query routing and conversation memory capabilities.
/// </remarks>
public sealed class MimirReasonerAgent : INemAgent
{
    /// <summary>
    /// The system prompt that defines the Reasoner agent's persona and capabilities.
    /// </summary>
    public const string ReasonerSystemPrompt =
        "You are the Reasoning Agent (Prefrontal Cortex). " +
        "You analyze queries, route to appropriate knowledge domains, and synthesize " +
        "multi-service information into coherent responses. " +
        "Maintain conversation context. Explain your reasoning process.";

    private const string DefaultModel = "qwen2.5:72b";

    private readonly ICognitiveClient _cognitiveClient;
    private readonly ConversationMemoryService _memoryService;
    private readonly QueryRouter _queryRouter;
    private readonly ILogger<MimirReasonerAgent> _logger;
    private readonly string _model;

    /// <summary>
    /// Initializes a new instance of the <see cref="MimirReasonerAgent"/> class.
    /// </summary>
    /// <param name="cognitiveClient">The cognitive client for LLM interactions.</param>
    /// <param name="memoryService">Conversation memory for maintaining context across turns.</param>
    /// <param name="queryRouter">Intent analysis and service routing.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="model">The LLM model to use. Defaults to "qwen2.5:72b".</param>
    public MimirReasonerAgent(
        ICognitiveClient cognitiveClient,
        ConversationMemoryService memoryService,
        QueryRouter queryRouter,
        ILogger<MimirReasonerAgent> logger,
        string? model = null)
    {
        _cognitiveClient = cognitiveClient ?? throw new ArgumentNullException(nameof(cognitiveClient));
        _memoryService = memoryService ?? throw new ArgumentNullException(nameof(memoryService));
        _queryRouter = queryRouter ?? throw new ArgumentNullException(nameof(queryRouter));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _model = model ?? DefaultModel;
    }

    /// <inheritdoc />
    public AgentIdentity Identity { get; } = new(
        ServiceName: "Mimir",
        AgentName: "Reasoner",
        SystemPrompt: ReasonerSystemPrompt,
        Description: "Prefrontal Cortex reasoner that analyzes queries, routes to knowledge domains, and synthesizes multi-service responses.");

    /// <inheritdoc />
    public AutonomyLevel Autonomy => AutonomyLevel.L0_Execute;

    /// <inheritdoc />
    public IReadOnlyList<ToolDescriptor> AvailableTools { get; } = CreateDefaultTools();

    /// <inheritdoc />
    public async Task<AgentResponse> InvokeAsync(
        string input,
        AgentContext? context = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input);

        var sw = Stopwatch.StartNew();

        _logger.LogDebug("MimirReasonerAgent invoked with input length {InputLength}", input.Length);

        // Analyze intent for routing metadata
        var intent = _queryRouter.AnalyzeIntent(input);
        _logger.LogDebug("Intent analysis: Category={Category}, Confidence={Confidence}, Service={Service}",
            intent.Category, intent.Confidence, intent.SuggestedService);

        // Build messages with conversation history from memory
        var messages = BuildMessages(input, context, intent);
        var options = new CognitiveOptions
        {
            MaxTokens = 4096,
            Temperature = 0.7,
            SystemPrompt = Identity.SystemPrompt,
        };

        var response = await _cognitiveClient.ChatAsync(_model, messages, options, ct)
            .ConfigureAwait(false);

        sw.Stop();

        // Store the conversation turn in memory
        var conversationId = context?.CorrelationId ?? Guid.NewGuid().ToString();
        _memoryService.AddUserMessage(conversationId, input, context?.UserId);
        _memoryService.AddAssistantMessage(conversationId, response.Content);

        _logger.LogDebug("MimirReasonerAgent completed in {Duration}ms using {Tokens} tokens. Routed to {Service}",
            sw.ElapsedMilliseconds, response.TotalTokens, intent.SuggestedService);

        return new AgentResponse(
            Content: response.Content,
            AgentId: Identity.AgentId,
            TokensUsed: response.TotalTokens,
            ToolCallCount: 0,
            Duration: sw.Elapsed,
            FinishReason: response.FinishReason);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<AgentStreamChunk> InvokeStreamingAsync(
        string input,
        AgentContext? context = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input);

        _logger.LogDebug("MimirReasonerAgent streaming invocation started");

        var intent = _queryRouter.AnalyzeIntent(input);

        var messages = BuildMessages(input, context, intent);
        var options = new CognitiveOptions
        {
            MaxTokens = 4096,
            Temperature = 0.7,
            SystemPrompt = Identity.SystemPrompt,
        };

        var conversationId = context?.CorrelationId ?? Guid.NewGuid().ToString();
        _memoryService.AddUserMessage(conversationId, input, context?.UserId);

        var fullResponse = new System.Text.StringBuilder();

        await foreach (var chunk in _cognitiveClient.ChatStreamingAsync(_model, messages, options, ct)
            .ConfigureAwait(false))
        {
            fullResponse.Append(chunk.Content);
            yield return new AgentStreamChunk(
                Content: chunk.Content,
                IsComplete: chunk.FinishReason is not null);
        }

        // Store the complete response in memory
        _memoryService.AddAssistantMessage(conversationId, fullResponse.ToString());
    }

    /// <inheritdoc />
    public Task EscalateIfUncertainAsync(double confidence, string reason, CancellationToken ct = default)
    {
        // HITL escalation is implemented in Task 31.
        _logger.LogWarning(
            "MimirReasonerAgent requested escalation: confidence={Confidence:F2}, reason={Reason}",
            confidence, reason);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Builds the message list for the cognitive client, incorporating conversation history
    /// from the agent context and enriching with routing metadata.
    /// </summary>
    private List<ChatMessage> BuildMessages(string input, AgentContext? context, IntentAnalysis intent)
    {
        var messages = new List<ChatMessage>();

        // Include conversation history if available
        if (context?.ConversationHistory is { Count: > 0 } history)
        {
            foreach (var msg in history)
            {
                messages.Add(new ChatMessage(msg.Role, msg.Content));
            }
        }
        else if (context?.CorrelationId is not null)
        {
            // Fall back to memory service for conversation history
            var memoryHistory = _memoryService.GetConversationHistory(context.CorrelationId);
            foreach (var msg in memoryHistory)
            {
                messages.Add(new ChatMessage(msg.Role, msg.Content));
            }
        }

        // Enrich the user input with routing context
        var enrichedInput = $"[Intent: {intent.Category} → {intent.SuggestedService} (confidence: {intent.Confidence:F2})]\n{input}";
        messages.Add(ChatMessage.User(enrichedInput));

        return messages;
    }

    private static IReadOnlyList<ToolDescriptor> CreateDefaultTools()
    {
        return new List<ToolDescriptor>
        {
            new(
                Name: "AnalyzeIntent",
                Description: "Analyze user query intent and determine the appropriate service domain for routing.",
                CommandType: typeof(MimirAgentCommands.AnalyzeIntent)),
            new(
                Name: "RouteToService",
                Description: "Route a query to a specific service domain based on intent analysis.",
                CommandType: typeof(MimirAgentCommands.RouteToService),
                ResponseType: typeof(RoutingResult)),
            new(
                Name: "ReasonAboutQuery",
                Description: "Invoke the reasoner to analyze and respond to a complex multi-domain query.",
                CommandType: typeof(MimirAgentCommands.ReasonAboutQuery)),
        }.AsReadOnly();
    }
}
