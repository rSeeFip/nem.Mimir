using nem.Contracts.Organism;
using Wolverine;

namespace nem.Mimir.Api.Federation;

public sealed class MimirFederationPeerHealthReporter(
    nem.Contracts.ControlPlane.IConfigurationManager configurationManager,
    ILogger<MimirFederationPeerHealthReporter> logger,
    IServiceScopeFactory serviceScopeFactory) : BackgroundService
{
    private const string IntervalKey = "Federation:PeerHealthIntervalSeconds";
    private readonly nem.Contracts.ControlPlane.IConfigurationManager _configurationManager = configurationManager;
    private readonly ILogger<MimirFederationPeerHealthReporter> _logger = logger;
    private readonly IServiceScopeFactory _serviceScopeFactory = serviceScopeFactory;

    public async Task PublishPeerHealthUpdateOnceAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

            var @event = new FederationPeerHealthUpdateEvent(
                DateTimeOffset.UtcNow,
                SourceServiceId: "nem.Mimir",
                CorrelationId: Guid.NewGuid(),
                PeerServiceId: "mimir",
                HealthScore: 1d,
                HealthStatus: "Healthy",
                Metrics: new Dictionary<string, double>
                {
                    ["federation_enabled"] = 1d,
                },
                Metadata: new Dictionary<string, string>
                {
                    ["service"] = "mimir",
                });

            await bus.PublishAsync(@event).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish Mimir federation peer health update.");
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = await GetIntervalAsync(stoppingToken).ConfigureAwait(false);
        using var timer = new PeriodicTimer(interval);

        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            await PublishPeerHealthUpdateOnceAsync(stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task<TimeSpan> GetIntervalAsync(CancellationToken cancellationToken)
    {
        var raw = await _configurationManager.GetConfigAsync("nem.Mimir", IntervalKey, cancellationToken).ConfigureAwait(false);
        return int.TryParse(raw, out var seconds) && seconds > 0
            ? TimeSpan.FromSeconds(seconds)
            : TimeSpan.FromSeconds(30);
    }
}
