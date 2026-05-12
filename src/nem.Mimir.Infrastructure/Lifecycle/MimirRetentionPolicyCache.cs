namespace nem.Mimir.Infrastructure.Lifecycle;

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using nem.Contracts.Lifecycle;

/// <summary>
/// Local retention policy cache for Mimir.
/// 
/// Unlike the MCP abstract <c>RetentionPolicyCache</c> (which lives in nem.MCP.Infrastructure
/// and is not referenceable from Mimir), this is a lightweight standalone cache that:
/// 1. Stores policy snapshots received via <see cref="RetentionPolicyChanged"/> events
/// 2. Provides lookup for the <see cref="MimirReadModelPruningStrategy"/>
/// 3. Falls back to sensible defaults when no explicit policy exists
/// 
/// Thread-safe via <see cref="ConcurrentDictionary{TKey,TValue}"/>.
/// </summary>
public sealed class MimirRetentionPolicyCache
{
    private const string ServiceName = "Mimir";

    /// <summary>
    /// Default hot retention: 90 days (conversations older than this are eligible for archiving).
    /// </summary>
    private static readonly TimeSpan DefaultHotRetention = TimeSpan.FromDays(90);

    /// <summary>
    /// Default cold retention: 365 days (archived conversations retained for 1 year before frozen).
    /// </summary>
    private static readonly TimeSpan DefaultColdRetention = TimeSpan.FromDays(365);

    /// <summary>
    /// Default frozen retention: 2555 days (~7 years, regulatory compliance).
    /// </summary>
    private static readonly TimeSpan DefaultFrozenRetention = TimeSpan.FromDays(2555);

    private readonly ConcurrentDictionary<string, CachedPolicy> _policies = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger _logger;

    public MimirRetentionPolicyCache(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<MimirRetentionPolicyCache>();

        // Seed with defaults for known Mimir data classes
        SeedDefaults();
    }

    /// <summary>
    /// Gets a retention policy snapshot for the specified data class.
    /// Returns defaults if no explicit policy has been received.
    /// </summary>
    public RetentionPolicySnapshot GetPolicy(string dataClass)
    {
        if (_policies.TryGetValue(BuildKey(dataClass), out var cached))
        {
            return cached.Snapshot;
        }

        // Return default policy for unknown data classes
        return BuildDefaultSnapshot(dataClass);
    }

    /// <summary>
    /// Gets all cached policies (explicit + defaults).
    /// </summary>
    public IReadOnlyList<RetentionPolicySnapshot> GetAllPolicies()
    {
        return _policies.Values
            .OrderBy(p => p.Snapshot.DataClass, StringComparer.OrdinalIgnoreCase)
            .Select(p => p.Snapshot)
            .ToList();
    }

    /// <summary>
    /// Updates the cache from an incoming <see cref="RetentionPolicyChanged"/> event.
    /// Called by <see cref="MimirRetentionPolicyChangedHandler"/>.
    /// </summary>
    public void UpdatePolicy(RetentionPolicyChanged message)
    {
        // Extract service and data class from ExtensionData if available
        if (!TryExtractPolicyDetails(message, out var dataClass, out var snapshot))
        {
            _logger.LogDebug(
                "Could not extract full policy details from RetentionPolicyChanged event {PolicyId}; " +
                "updating retention days only for generic cache entry",
                message.RetentionPolicyId);

            // Fallback: update a generic entry using RetentionDays as hot retention
            var genericKey = BuildKey("Default");
            var hotRetention = TimeSpan.FromDays(message.RetentionDays);

            _policies[genericKey] = new CachedPolicy(
                new RetentionPolicySnapshot(
                    ServiceId: ServiceName,
                    DataClass: "Default",
                    Kind: StorageKind.EfReadModel.ToString(),
                    HotRetention: hotRetention,
                    ColdRetention: DefaultColdRetention,
                    FrozenRetention: DefaultFrozenRetention,
                    RequiresGdprHandling: true),
                DateTimeOffset.UtcNow);
            return;
        }

        if (!message.IsActive)
        {
            // Policy deactivated — revert to defaults
            var key = BuildKey(dataClass);
            _policies[key] = new CachedPolicy(BuildDefaultSnapshot(dataClass), DateTimeOffset.UtcNow);

            _logger.LogInformation(
                "Reverted retention policy for {DataClass} to defaults after deactivation",
                dataClass);
            return;
        }

        _policies[BuildKey(dataClass)] = new CachedPolicy(snapshot, DateTimeOffset.UtcNow);

        _logger.LogInformation(
            "Cached retention policy for {DataClass}: Hot={HotDays}d, Cold={ColdDays}d, Frozen={FrozenDays}d",
            dataClass,
            snapshot.HotRetention.TotalDays,
            snapshot.ColdRetention.TotalDays,
            snapshot.FrozenRetention.TotalDays);
    }

    private void SeedDefaults()
    {
        var dataClasses = new[] { "Conversations", "Messages", "Users" };

        foreach (var dataClass in dataClasses)
        {
            _policies[BuildKey(dataClass)] = new CachedPolicy(
                BuildDefaultSnapshot(dataClass),
                DateTimeOffset.UtcNow);
        }
    }

    private static RetentionPolicySnapshot BuildDefaultSnapshot(string dataClass)
    {
        return new RetentionPolicySnapshot(
            ServiceId: ServiceName,
            DataClass: dataClass,
            Kind: StorageKind.EfReadModel.ToString(),
            HotRetention: DefaultHotRetention,
            ColdRetention: DefaultColdRetention,
            FrozenRetention: DefaultFrozenRetention,
            RequiresGdprHandling: true);
    }

    private static bool TryExtractPolicyDetails(
        RetentionPolicyChanged message,
        out string dataClass,
        out RetentionPolicySnapshot snapshot)
    {
        dataClass = string.Empty;
        snapshot = default!;

        if (message.ExtensionData is null)
            return false;

        if (!TryReadString(message.ExtensionData, "dataClass", out dataClass))
            return false;

        var hotRetention = TryReadTimeSpanOrDefault(message.ExtensionData, "hotRetention", TimeSpan.FromDays(message.RetentionDays));
        var coldRetention = TryReadTimeSpanOrDefault(message.ExtensionData, "coldRetention", DefaultColdRetention);
        var frozenRetention = TryReadTimeSpanOrDefault(message.ExtensionData, "frozenRetention", DefaultFrozenRetention);
        var requiresGdpr = TryReadBoolOrDefault(message.ExtensionData, "requiresGdprHandling", true);

        _ = TryReadString(message.ExtensionData, "kind", out var kind);
        if (string.IsNullOrWhiteSpace(kind))
            kind = StorageKind.EfReadModel.ToString();

        snapshot = new RetentionPolicySnapshot(
            ServiceId: ServiceName,
            DataClass: dataClass,
            Kind: kind,
            HotRetention: hotRetention,
            ColdRetention: coldRetention,
            FrozenRetention: frozenRetention,
            RequiresGdprHandling: requiresGdpr);

        return true;
    }

    private static bool TryReadString(
        Dictionary<string, System.Text.Json.JsonElement> extensionData,
        string key,
        out string value)
    {
        value = string.Empty;
        if (!extensionData.TryGetValue(key, out var element)
            || element.ValueKind != System.Text.Json.JsonValueKind.String)
            return false;

        value = element.GetString() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(value);
    }

    private static TimeSpan TryReadTimeSpanOrDefault(
        Dictionary<string, System.Text.Json.JsonElement> extensionData,
        string key,
        TimeSpan defaultValue)
    {
        if (!extensionData.TryGetValue(key, out var element))
            return defaultValue;

        try
        {
            var options = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web);
            var value = System.Text.Json.JsonSerializer.Deserialize<TimeSpan>(element.GetRawText(), options);
            return value > TimeSpan.Zero ? value : defaultValue;
        }
        catch
        {
            return defaultValue;
        }
    }

    private static bool TryReadBoolOrDefault(
        Dictionary<string, System.Text.Json.JsonElement> extensionData,
        string key,
        bool defaultValue)
    {
        if (!extensionData.TryGetValue(key, out var element))
            return defaultValue;

        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<bool>(element.GetRawText());
        }
        catch
        {
            return defaultValue;
        }
    }

    private static string BuildKey(string dataClass) => $"{ServiceName}::{dataClass.Trim()}";

    private sealed record CachedPolicy(RetentionPolicySnapshot Snapshot, DateTimeOffset LastUpdated);
}
