using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using nem.Cognitive.Abstractions.Interfaces;
using nem.Cognitive.Agents.Interfaces;
using Mimir.Application.Agents;

namespace Mimir.Infrastructure.Agents;

/// <summary>
/// Configuration options for the Mimir Reasoner Agent.
/// Bound from the "MimirReasonerAgent" configuration section.
/// </summary>
public sealed class MimirReasonerAgentOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "MimirReasonerAgent";

    /// <summary>The LLM model identifier to use.</summary>
    public string Model { get; set; } = "qwen2.5:72b";

    /// <summary>Maximum number of conversation messages to include in context.</summary>
    public int ContextWindow { get; set; } = 20;

    /// <summary>Whether the agent is enabled.</summary>
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// DI registration extension for the Mimir Reasoner Agent.
/// Follows the AddNem*() naming convention established across the nem.* platform.
/// </summary>
public static class MimirAgentRegistration
{
    /// <summary>
    /// Registers the Mimir Reasoner Agent and its dependencies in the DI container.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddNemMimirReasonerAgent(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Bind configuration
        services.Configure<MimirReasonerAgentOptions>(
            configuration.GetSection(MimirReasonerAgentOptions.SectionName));

        var options = configuration
            .GetSection(MimirReasonerAgentOptions.SectionName)
            .Get<MimirReasonerAgentOptions>() ?? new MimirReasonerAgentOptions();

        // Register QueryRouter (singleton — stateless keyword analysis)
        services.AddSingleton<QueryRouter>();

        // Register ConversationMemoryService (singleton — holds in-memory conversation state)
        services.AddSingleton(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<ConversationMemoryService>>();
            return new ConversationMemoryService(logger, options.ContextWindow);
        });

        // Register MimirReasonerAgent as INemAgent (scoped — depends on scoped ICognitiveClient if available)
        services.AddSingleton<INemAgent>(sp =>
        {
            var cognitiveClient = sp.GetRequiredService<ICognitiveClient>();
            var memoryService = sp.GetRequiredService<ConversationMemoryService>();
            var queryRouter = sp.GetRequiredService<QueryRouter>();
            var logger = sp.GetRequiredService<ILogger<MimirReasonerAgent>>();

            return new MimirReasonerAgent(
                cognitiveClient,
                memoryService,
                queryRouter,
                logger,
                options.Model);
        });

        return services;
    }
}
