#nullable enable

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using nem.Contracts.AspNetCore.Organism.MapeK;
using nem.Contracts.Organism;

namespace nem.Mimir.Infrastructure.Organism.MapeK;

/// <summary>
/// Registers Mimir's MAPE-K integration with conversational health playbooks.
/// Composes the shared MAPE-K config agent with Mimir-specific playbooks
/// for conversational AI health management.
/// </summary>
public static class MimirMapeKRegistration
{
    public static IServiceCollection AddMimirMapeK(this IServiceCollection services)
    {
        services.AddNemMapeKConfigAgent("nem.Mimir", AutonomyLevel.L1_Suggest);
        services.Replace(ServiceDescriptor.Singleton(_ => new MapeKPlanner(MimirMapeKPlaybooks.Create())));
        return services;
    }
}
