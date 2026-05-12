namespace nem.Mimir.Infrastructure.Plugins.BuiltIn;

internal sealed class SkillsMarketplaceOptions
{
    public const string SectionName = "SkillsMarketplace";

    public string? BaseUrl { get; init; }

    public int TimeoutSeconds { get; init; } = 30;
}
