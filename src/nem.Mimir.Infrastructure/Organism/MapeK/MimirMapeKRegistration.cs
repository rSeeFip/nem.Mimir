#nullable enable

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using nem.Contracts.Organism;
using nem.Contracts.AspNetCore.Organism.MapeK;
using nem.Contracts.Identity;

namespace nem.Mimir.Infrastructure.Organism.MapeK;

public static class MimirMapeKRegistration
{
    public static IServiceCollection AddMimirMapeK(this IServiceCollection services)
    {
        services.AddNemMapeKConfigAgent("nem.Mimir", AutonomyLevel.L1_Suggest);
        services.Replace(ServiceDescriptor.Singleton(_ => new MapeKPlanner(MimirMapeKPlaybooks.Create())));
        return services;
    }
}
