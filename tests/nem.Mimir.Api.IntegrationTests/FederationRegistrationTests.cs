using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using nem.Contracts.AspNetCore.Messaging.DeadLetter;
using nem.Contracts.Federation.DeadLetter;
using nem.Contracts.Organism;
using nem.Mimir.Api.Federation;
using Wolverine;
using Wolverine.Runtime;

namespace nem.Mimir.Api.IntegrationTests;

public sealed class FederationRegistrationTests
{

    [Fact]
    public void FederationServices_AreRegisteredInTheServiceCollection()
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddMimirFederation(BuildConfiguration());

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var serviceProvider = scope.ServiceProvider;

        serviceProvider.GetRequiredService<nem.Contracts.ControlPlane.IConfigurationManager>().ShouldNotBeNull();
        serviceProvider.GetRequiredService<MimirFederationAbacMiddleware>().ShouldNotBeNull();
        serviceProvider.GetRequiredService<MimirFederationPeerHealthState>().ShouldNotBeNull();
        serviceProvider.GetRequiredService<MimirFederationPeerHealthReporter>().ShouldNotBeNull();
        serviceProvider.GetRequiredService<FederationDeadLetterConsumer>().QueueName.ShouldBe("mimir.dead-letter");
        var replayRegistration = services.SingleOrDefault(sd => sd.ServiceType == typeof(ReplayDeadLetterHandler));
        replayRegistration.ShouldNotBeNull();
        replayRegistration!.Lifetime.ShouldBe(ServiceLifetime.Scoped);
    }

    [Fact]
    public void FederationHandlers_AreDiscoveredByWolverine()
    {
        using var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddLogging();
                services.AddMimirFederation(BuildConfiguration());
            })
            .UseWolverine(options =>
            {
                options.Discovery.IncludeAssembly(typeof(MimirFederationPeerHealthUpdateHandler).Assembly);
                options.Discovery.IncludeAssembly(typeof(ReplayDeadLetterHandler).Assembly);
            })
            .StartAsync().GetAwaiter().GetResult();

        var runtime = (WolverineRuntime)host.Services.GetRequiredService<IWolverineRuntime>();
        var messageTypes = runtime.Handlers.Chains
            .Select(chain => chain.MessageType)
            .Where(type => type is not null)
            .ToArray();

        messageTypes.ShouldContain(typeof(FederationPeerHealthUpdateEvent));
        messageTypes.ShouldContain(typeof(ReplayDeadLetterCommand));
    }

    private static IConfiguration BuildConfiguration()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();
    }
}
