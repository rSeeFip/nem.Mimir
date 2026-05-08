namespace nem.Mimir.Domain.Tenants;

public sealed class TenantConfiguration
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public int RateLimitPerMinute { get; set; } = 100;
    public string[] AllowedModels { get; set; } = [];
    public string[] AllowedTools { get; set; } = [];
    public Dictionary<string, string> FeatureFlags { get; set; } = new();
    public DateTimeOffset UpdatedAt { get; set; }
}
