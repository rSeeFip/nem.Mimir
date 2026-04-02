#nullable enable

using Microsoft.Extensions.DependencyInjection;
using nem.Contracts.AspNetCore.Organism;
using nem.Contracts.Organism;
using nem.Mimir.Infrastructure.Organism.Heartbeat;
using nem.Mimir.Infrastructure.Organism.MapeK;

namespace nem.Mimir.Infrastructure.Organism;

/// <summary>
/// Registers Mimir's organism integration services.
/// Wires heartbeat participation, cross-service event subscription,
/// and conversational health monitoring into the organism subsystem.
/// Wolverine handlers are auto-discovered from this assembly.
/// </summary>
public static class MimirOrganismRegistration
{
    public static IServiceCollection AddMimirOrganism(this IServiceCollection services)
    {
        // Register organism heartbeat infrastructure from T4 shared extensions
        services.AddOrganismHeartbeat(opts =>
        {
            opts.Interval = TimeSpan.FromSeconds(15);
            opts.Timeout = TimeSpan.FromSeconds(45);
        });

        // Mimir-specific organism implementations
        services.AddSingleton<IOrganismHeartbeat, MimirOrganismHeartbeat>();

        // Mimir conversational health analyzer (MAPE-K)
        services.AddSingleton<ConversationalHealthAnalyzer>();

        // Mimir MAPE-K registration (extends shared MapeK with Mimir-specific playbooks)
        services.AddMimirMapeK();

        // NOTE: Wolverine handlers (HeartbeatPulseHandler, OrganismStateChangedHandler,
        // FederationPeerHealthUpdateHandler, OrganismSubsystemAlertHandler) are auto-discovered
        // by Wolverine from the nem.Mimir.Infrastructure assembly — no explicit registration needed.

        return services;
    }
}
