using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using nem.Mimir.Sync.Publishers;
using Wolverine;
using Wolverine.RabbitMQ;

namespace nem.Mimir.Sync.Configuration;

/// <summary>
/// Extension methods for configuring Wolverine messaging in the Mimir application.
/// </summary>
public static class WolverineConfiguration
{

    /// <summary>
    /// Configures Wolverine messaging with RabbitMQ transport, durable messaging, and Mimir handler discovery.
    /// </summary>
    /// <param name="opts">The Wolverine options to configure.</param>
    /// <param name="configuration">The application configuration containing RabbitMQ settings.</param>
    /// <returns>The configured <see cref="WolverineOptions"/> for chaining.</returns>
    public static WolverineOptions AddMimirMessaging(this WolverineOptions opts, IConfiguration configuration)
    {
        var connectionString = configuration["RabbitMQ:ConnectionString"]
            ?? throw new InvalidOperationException("RabbitMQ:ConnectionString configuration is required.");

        opts.UseRabbitMq(new Uri(connectionString))
            .AutoProvision()
            .EnableWolverineControlQueues();

        opts.Policies.UseDurableInboxOnAllListeners();
        opts.Policies.UseDurableOutboxOnAllSendingEndpoints();

        opts.Discovery.IncludeAssembly(typeof(WolverineConfiguration).Assembly);

        opts.Services.AddScoped<IMimirEventPublisher, MimirEventPublisher>();

        return opts;
    }
}
