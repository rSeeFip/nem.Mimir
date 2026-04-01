using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using nem.Contracts.Agents;
using nem.Mimir.Application.Agents.Selection;
using nem.Mimir.Application.Agents.Selection.Steps;

namespace nem.Mimir.Application.Agents;

public static class AgentServiceCollectionExtensions
{
    public static IServiceCollection AddAgentOrchestration(this IServiceCollection services)
    {
        services.AddScoped<ISelectionStep, QualityFilterStep>();
        services.AddScoped<ISelectionStep, Bm25RankerStep>();
        services.AddScoped<ISelectionStep, EmbeddingRankerStep>();
        services.TryAddScoped<ISkillQualityProvider, DefaultSkillQualityProvider>();

        services.AddScoped<AgentDispatcher>();
        services.AddScoped<AgentCoordinator>();
        services.TryAddSingleton<TierConfiguration>();
        services.TryAddSingleton<TierDispatchStrategy>();
        services.TryAddSingleton<ConfidenceEscalationPolicy>();
        services.AddScoped<IAgentOrchestrator, AgentOrchestrator>();

        return services;
    }

    private sealed class DefaultSkillQualityProvider : ISkillQualityProvider
    {
        public Task<SkillQualityInfo?> GetQualityAsync(string agentName, CancellationToken ct = default)
        {
            return Task.FromResult<SkillQualityInfo?>(null);
        }
    }
}
