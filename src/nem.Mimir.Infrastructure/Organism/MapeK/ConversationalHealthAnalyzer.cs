#nullable enable

using Microsoft.Extensions.Logging;

namespace nem.Mimir.Infrastructure.Organism.MapeK;

/// <summary>
/// Mimir-specific MAPE-K observation provider that enriches organism health monitoring
/// with conversational AI metrics (response latency, memory usage, session health,
/// LLM provider availability).
/// These metrics flow through the MapeKConfigAgent -> MapeKAnalyzer pipeline.
/// </summary>
public sealed class ConversationalHealthAnalyzer
{
    private readonly ILogger<ConversationalHealthAnalyzer> _logger;

    // Mimir-specific thresholds for MAPE-K anomaly detection
    internal const double HighResponseLatencyMs = 5000d;
    internal const double HighMemoryUsageMb = 1024d;
    internal const double LowSessionHealthScore = 0.5d;
    internal const double HighLlmErrorRate = 0.1d;

    public ConversationalHealthAnalyzer(ILogger<ConversationalHealthAnalyzer> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Collects Mimir-specific observations that enrich the homeostasis diagnostics.
    /// These observations are consumed by the MapeKConfigAgent during its Monitor phase.
    /// In a full implementation, these would come from real service metrics.
    /// </summary>
    public IReadOnlyDictionary<string, double> CollectObservations()
    {
        // Placeholder observations — in production these would come from
        // actual service metrics (response times, session counts, memory telemetry).
        var observations = new Dictionary<string, double>(StringComparer.Ordinal)
        {
            ["mimir_response_latency_ms"] = 0d,
            ["mimir_memory_usage_mb"] = 0d,
            ["mimir_session_health_score"] = 1.0d,
            ["mimir_active_sessions"] = 0d,
            ["mimir_llm_error_rate"] = 0d
        };

        _logger.LogDebug(
            "Mimir MAPE-K observations collected: latency={Latency}ms, memory={Memory}MB, sessionHealth={SessionHealth:F2}",
            observations["mimir_response_latency_ms"],
            observations["mimir_memory_usage_mb"],
            observations["mimir_session_health_score"]);

        return observations;
    }

    /// <summary>
    /// Analyzes Mimir-specific observations and returns detected anomalies.
    /// Anomaly names match MimirMapeKPlaybooks triggers for automatic planning.
    /// </summary>
    public IReadOnlyList<MimirMapeKAnomaly> Analyze(IReadOnlyDictionary<string, double> observations)
    {
        ArgumentNullException.ThrowIfNull(observations);

        var anomalies = new List<MimirMapeKAnomaly>();

        if (TryGetValue(observations, "mimir_response_latency_ms", out var latency)
            && latency >= HighResponseLatencyMs)
        {
            anomalies.Add(new MimirMapeKAnomaly(
                Name: "high-response-latency",
                Metric: "mimir_response_latency_ms",
                Value: latency,
                Reason: $"Response latency ({latency}ms) exceeded threshold ({HighResponseLatencyMs}ms)"));
        }

        if (TryGetValue(observations, "mimir_memory_usage_mb", out var memory)
            && memory >= HighMemoryUsageMb)
        {
            anomalies.Add(new MimirMapeKAnomaly(
                Name: "high-memory-usage",
                Metric: "mimir_memory_usage_mb",
                Value: memory,
                Reason: $"Memory usage ({memory}MB) exceeded threshold ({HighMemoryUsageMb}MB)"));
        }

        if (TryGetValue(observations, "mimir_session_health_score", out var sessionHealth)
            && sessionHealth <= LowSessionHealthScore)
        {
            anomalies.Add(new MimirMapeKAnomaly(
                Name: "session-health-degradation",
                Metric: "mimir_session_health_score",
                Value: sessionHealth,
                Reason: $"Session health score ({sessionHealth:F2}) fell below threshold ({LowSessionHealthScore})"));
        }

        if (TryGetValue(observations, "mimir_llm_error_rate", out var errorRate)
            && errorRate >= HighLlmErrorRate)
        {
            anomalies.Add(new MimirMapeKAnomaly(
                Name: "llm-provider-error-spike",
                Metric: "mimir_llm_error_rate",
                Value: errorRate,
                Reason: $"LLM error rate ({errorRate:F3}) exceeded threshold ({HighLlmErrorRate})"));
        }

        return anomalies;
    }

    private static bool TryGetValue(
        IReadOnlyDictionary<string, double> observations,
        string key,
        out double value)
    {
        return observations.TryGetValue(key, out value);
    }
}

/// <summary>
/// Mimir-specific MAPE-K anomaly detected by the conversational health analyzer.
/// Names match MimirMapeKPlaybooks triggers for automatic planning.
/// </summary>
public sealed record MimirMapeKAnomaly(
    string Name,
    string Metric,
    double Value,
    string Reason);
