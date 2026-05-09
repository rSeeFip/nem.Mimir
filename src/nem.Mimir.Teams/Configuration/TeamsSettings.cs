namespace nem.Mimir.Teams.Configuration;

internal sealed class TeamsSettings
{
    public const string SectionName = "Teams";

    public string AppId { get; set; } = string.Empty;

    public string AppPassword { get; set; } = string.Empty;

    public string? TenantId { get; set; }
}
