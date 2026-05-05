namespace nem.Mimir.Infrastructure.Observability;

using System.Diagnostics.Metrics;

/// <summary>
/// OpenTelemetry-compatible metrics for Mimir.
/// Uses System.Diagnostics.Metrics (built-in .NET) — no PII in labels.
/// </summary>
public sealed class MimirMetrics : IDisposable
{
    public const string MeterName = "nem.Mimir";

    private readonly Meter _meter;

    /// <summary>Total tokens processed. Labels: direction (input|output), model.</summary>
    public readonly Counter<long> TokensTotal;

    /// <summary>Total cost in USD. Labels: tenant_id, model.</summary>
    public readonly Counter<double> CostTotal;

    /// <summary>Number of requests denied due to budget limits. Labels: tenant_id.</summary>
    public readonly Counter<long> BudgetDeniedTotal;

    /// <summary>Number of requests denied by guardrail rules. Labels: bundle.</summary>
    public readonly Counter<long> GuardrailDeniedTotal;

    public MimirMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);
        _meter = meterFactory.Create(MeterName);

        TokensTotal = _meter.CreateCounter<long>(
            name: "mimir_tokens_total",
            unit: "tokens",
            description: "Total number of tokens processed by Mimir.");

        CostTotal = _meter.CreateCounter<double>(
            name: "mimir_cost_total",
            unit: "USD",
            description: "Total cost in USD incurred by Mimir LLM calls.");

        BudgetDeniedTotal = _meter.CreateCounter<long>(
            name: "mimir_budget_denied_total",
            unit: "requests",
            description: "Total number of requests denied due to budget limits.");

        GuardrailDeniedTotal = _meter.CreateCounter<long>(
            name: "mimir_guardrail_denied_total",
            unit: "requests",
            description: "Total number of requests denied by guardrail rules.");
    }

    /// <summary>Record token usage. No PII — only direction and model labels.</summary>
    public void RecordTokens(long inputTokens, long outputTokens, string model)
    {
        if (inputTokens > 0)
        {
            TokensTotal.Add(inputTokens,
                new KeyValuePair<string, object?>("direction", "input"),
                new KeyValuePair<string, object?>("model", model ?? "unknown"));
        }

        if (outputTokens > 0)
        {
            TokensTotal.Add(outputTokens,
                new KeyValuePair<string, object?>("direction", "output"),
                new KeyValuePair<string, object?>("model", model ?? "unknown"));
        }
    }

    /// <summary>Record cost. No PII — only tenant_id and model labels (no user_id).</summary>
    public void RecordCost(double costUsd, string tenantId, string model)
    {
        if (costUsd > 0)
        {
            CostTotal.Add(costUsd,
                new KeyValuePair<string, object?>("tenant_id", tenantId ?? "unknown"),
                new KeyValuePair<string, object?>("model", model ?? "unknown"));
        }
    }

    /// <summary>Record a budget denial. No PII — only tenant_id label.</summary>
    public void RecordBudgetDenied(string tenantId)
    {
        BudgetDeniedTotal.Add(1,
            new KeyValuePair<string, object?>("tenant_id", tenantId ?? "unknown"));
    }

    /// <summary>Record a guardrail denial. No PII — only bundle label.</summary>
    public void RecordGuardrailDenied(string bundle)
    {
        GuardrailDeniedTotal.Add(1,
            new KeyValuePair<string, object?>("bundle", bundle ?? "unknown"));
    }

    public void Dispose() => _meter.Dispose();
}
