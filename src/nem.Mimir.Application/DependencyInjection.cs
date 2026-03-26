using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using nem.Mimir.Application.Common.Mappings;
using nem.Mimir.Application.Agents;
using nem.Mimir.Application.Tasks;
using nem.Contracts.Memory;

namespace nem.Mimir.Application;

/// <summary>
/// Extension methods for registering Application layer services in the DI container.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Adds Application layer services including FluentValidation and Mapperly.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        services.AddSingleton<MimirMapper>();

        services.AddValidatorsFromAssembly(assembly);

        services.AddAgentOrchestration();
        services.AddBackgroundTaskExecution();
        services.AddSingleton<global::nem.Mimir.Application.Services.Memory.WorkingMemoryOptions>();
        services.AddScoped<IWorkingMemory, global::nem.Mimir.Application.Services.Memory.WorkingMemoryService>();
        services.AddSingleton<global::nem.Mimir.Application.Services.Memory.EpisodicMemoryOptions>();
        services.AddScoped<nem.Contracts.Memory.IEpisodicMemory, global::nem.Mimir.Application.Services.Memory.EpisodicMemoryService>();
        services.AddSingleton<global::nem.Mimir.Application.Services.Memory.SemanticMemoryOptions>();
        services.AddSingleton<global::nem.Mimir.Application.Services.Memory.ISemanticFactRepository, global::nem.Mimir.Application.Services.Memory.InMemorySemanticFactRepository>();
        // T25: Semantic Memory
        services.AddScoped<nem.Contracts.Memory.ISemanticMemory, global::nem.Mimir.Application.Services.Memory.SemanticMemoryService>();
        services.AddSingleton<global::nem.Mimir.Application.Services.Memory.MemoryConsolidationOptions>();
        services.AddScoped<nem.Contracts.Memory.IMemoryConsolidator, global::nem.Mimir.Application.Services.Memory.MemoryConsolidationService>();
        // T27: Agent Memory Context
        services.AddSingleton<global::nem.Mimir.Application.Agents.AgentMemoryContextOptions>();
        services.AddScoped<global::nem.Mimir.Application.Agents.IAgentMemoryContextService, global::nem.Mimir.Application.Agents.AgentMemoryContextService>();
        // T28: Context Optimizer
        services.AddSingleton<global::nem.Mimir.Application.Context.ContextOptimizerOptions>();
        services.AddScoped<nem.Contracts.TokenOptimization.IContextOptimizer, global::nem.Mimir.Application.Context.ContextOptimizerService>();
        services.AddSingleton(sp => Microsoft.Extensions.Options.Options.Create(
            new global::nem.Mimir.Application.Context.PromptCompressionOptions()));
        services.AddScoped<global::nem.Mimir.Application.Context.PromptCompressionService>();
        // T31: Token Tracker
        services.AddSingleton<global::nem.Mimir.Application.Tokens.TokenTrackerOptions>();
        services.AddSingleton<nem.Contracts.TokenOptimization.ITokenTracker, global::nem.Mimir.Application.Tokens.TokenTrackerService>();
        // T30: Model Cascade
        services.AddSingleton<global::nem.Mimir.Application.Llm.ModelCascadeOptions>();
        services.AddSingleton<nem.Contracts.TokenOptimization.IModelCascade, global::nem.Mimir.Application.Llm.ModelCascadeService>();
        // T23: MCP Tool Catalog
        services.AddSingleton<global::nem.Mimir.Application.Mcp.McpToolCatalogOptions>();
        services.AddSingleton<global::nem.Mimir.Application.Mcp.IPersonaToolFilter, global::nem.Mimir.Application.Mcp.DefaultPersonaToolFilter>();
        services.AddScoped<global::nem.Mimir.Application.Mcp.IMcpToolCatalogService, global::nem.Mimir.Application.Mcp.McpToolCatalogService>();

        return services;
    }
}
