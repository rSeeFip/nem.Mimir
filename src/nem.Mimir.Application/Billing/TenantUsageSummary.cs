namespace nem.Mimir.Application.Billing;

public class TenantUsageSummary
{
    public string TenantId { get; set; } = string.Empty;

    public DateTimeOffset PeriodStart { get; set; }

    public DateTimeOffset PeriodEnd { get; set; }

    public long TotalInputTokens { get; set; }

    public long TotalOutputTokens { get; set; }

    public decimal TotalCost { get; set; }

    public int RequestCount { get; set; }

    public Dictionary<string, ModelUsage> ModelBreakdown { get; set; } = new();
}
