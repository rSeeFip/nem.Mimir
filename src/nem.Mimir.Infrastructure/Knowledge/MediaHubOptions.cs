namespace nem.Mimir.Infrastructure.Knowledge;

public sealed class MediaHubOptions
{
    public const string SectionName = "MediaHub";

    public string BaseUrl { get; set; } = "http://localhost:5005";

    public int TimeoutSeconds { get; set; } = 30;

    public bool Enabled { get; set; }
}
