namespace nem.Mimir.Infrastructure.Knowledge;

public sealed class SearxngOptions
{
    public const string SectionName = "SearXng";

    public string BaseUrl { get; set; } = "http://localhost:8081";

    public int TimeoutSeconds { get; set; } = 10;

    public int MaxResults { get; set; } = 8;
}
