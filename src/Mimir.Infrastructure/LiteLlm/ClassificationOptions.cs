namespace Mimir.Infrastructure.LiteLlm;

public sealed class ClassificationOptions
{
    public const string SectionName = "Classification";

    public string ClassificationApiBaseUrl { get; set; } = "http://localhost:5100";

    public HashSet<string> InternalHosts { get; set; } =
    [
        "localhost",
        "ollama",
        "litellm",
    ];
}
