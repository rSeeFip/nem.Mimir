namespace nem.Mimir.Infrastructure.Tests.Federation;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using nem.Contracts.ControlPlane;
using nem.Contracts.Organism;
using nem.Mimir.Api.Federation;
using Wolverine;

public sealed class MimirFederationHealthTests
{
    [Fact]
    public async Task FederationHealthReporter_PublishesPeerHealthUpdateEvent()
    {
        var configurationManager = Substitute.For<IConfigurationManager>();
        var logger = Substitute.For<ILogger<MimirFederationPeerHealthReporter>>();
        var messageBus = Substitute.For<IMessageBus>();
        var services = new ServiceCollection();
        services.AddSingleton(messageBus);
        using var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        var sut = new MimirFederationPeerHealthReporter(configurationManager, logger, scopeFactory);

        await sut.PublishPeerHealthUpdateOnceAsync();

        await messageBus.Received(1).PublishAsync(Arg.Is<FederationPeerHealthUpdateEvent>(evt =>
            evt.SourceServiceId == "nem.Mimir"
            && evt.PeerServiceId == "mimir"
            && evt.HealthStatus == "Healthy"
            && evt.Metrics != null
            && evt.Metrics["federation_enabled"] == 1d));
    }

    [Fact]
    public async Task FederationHealthHandler_StoresLatestPeerHealthUpdate()
    {
        var state = new MimirFederationPeerHealthState();
        var logger = Substitute.For<ILogger<MimirFederationPeerHealthUpdateHandler>>();
        var sut = new MimirFederationPeerHealthUpdateHandler();
        var update = new FederationPeerHealthUpdateEvent(
            DateTimeOffset.UtcNow,
            "nem.Backup",
            Guid.NewGuid(),
            "backup",
            0.5d,
            "Degraded");

        await sut.Handle(update, state, logger, CancellationToken.None);

        state.TryGet("backup", out var stored).ShouldBeTrue();
        stored.ShouldBe(update);
        state.Count.ShouldBe(1);
    }
}
