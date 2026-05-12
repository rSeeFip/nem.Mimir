using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection.Extensions;
using nem.Contracts.AspNetCore.Federation;
using nem.Contracts.AspNetCore.Messaging.DeadLetter;

namespace nem.Mimir.Api.Federation;

public static class MimirFederationServiceCollectionExtensions
{
    public static IServiceCollection AddMimirFederation(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddFederationFeatureFlags(configuration);
        services.TryAddSingleton<nem.Contracts.ControlPlane.IConfigurationManager, MimirInMemoryConfigurationManager>();
        services.AddScoped<MimirFederationAbacContextAccessor>();
        services.AddScoped<MimirFederationAbacMiddleware>();
        services.AddSingleton(new FederationDeadLetterConsumer("mimir"));
        services.AddScoped<ReplayDeadLetterHandler>();
        services.AddSingleton<MimirFederationPeerHealthState>();
        services.AddSingleton<MimirFederationPeerHealthReporter>();
        services.AddHostedService(sp => sp.GetRequiredService<MimirFederationPeerHealthReporter>());

        return services;
    }

    private sealed class MimirInMemoryConfigurationManager : nem.Contracts.ControlPlane.IConfigurationManager
    {
        private readonly ConcurrentDictionary<string, string> _serviceConfigs = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, string> _globalConfigs = new(StringComparer.OrdinalIgnoreCase);

        public Task<string?> GetConfigAsync(string serviceId, string key, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _serviceConfigs.TryGetValue(ToServiceKey(serviceId, key), out var value);
            return Task.FromResult<string?>(value);
        }

        public Task SetConfigAsync(string serviceId, string key, string value, bool isSecret = false, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _serviceConfigs[ToServiceKey(serviceId, key)] = value;
            return Task.CompletedTask;
        }

        public Task<string?> GetGlobalConfigAsync(string key, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _globalConfigs.TryGetValue(key, out var value);
            return Task.FromResult<string?>(value);
        }

        public Task SetGlobalConfigAsync(string key, string value, bool isSecret = false, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _globalConfigs[key] = value;
            return Task.CompletedTask;
        }

        public Task BulkUpdateAsync(string serviceId, IReadOnlyDictionary<string, string> configs, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var (key, value) in configs)
            {
                _serviceConfigs[ToServiceKey(serviceId, key)] = value;
            }

            return Task.CompletedTask;
        }

        private static string ToServiceKey(string serviceId, string key) => $"{serviceId}:{key}";
    }
}
