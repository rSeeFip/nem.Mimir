using System.Reflection;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Mimir.Application.Common.Behaviours;
using Mimir.Application.Common.Mappings;
using Mimir.Application.Agents;
using Mimir.Application.Tasks;
using nem.Contracts.Memory;

namespace Mimir.Application;

/// <summary>
/// Extension methods for registering Application layer services in the DI container.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Adds Application layer services including MediatR, FluentValidation, and Mapperly.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        services.AddSingleton<MimirMapper>();

        services.AddValidatorsFromAssembly(assembly);

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);

            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(UnhandledExceptionBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(PerformanceBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        });

        services.AddAgentOrchestration();
        services.AddBackgroundTaskExecution();
        services.AddSingleton<global::Mimir.Application.Services.Memory.WorkingMemoryOptions>();
        services.AddScoped<IWorkingMemory, global::Mimir.Application.Services.Memory.WorkingMemoryService>();
        services.AddSingleton<global::Mimir.Application.Services.Memory.EpisodicMemoryOptions>();
        services.AddScoped<nem.Contracts.Memory.IEpisodicMemory, global::Mimir.Application.Services.Memory.EpisodicMemoryService>();
        services.AddSingleton<global::Mimir.Application.Services.Memory.SemanticMemoryOptions>();
        services.AddSingleton<global::Mimir.Application.Services.Memory.ISemanticFactRepository, global::Mimir.Application.Services.Memory.InMemorySemanticFactRepository>();
        // T25: Semantic Memory
        services.AddScoped<nem.Contracts.Memory.ISemanticMemory, global::Mimir.Application.Services.Memory.SemanticMemoryService>();

        return services;
    }
}
