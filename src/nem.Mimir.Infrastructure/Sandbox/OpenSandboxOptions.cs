namespace nem.Mimir.Infrastructure.Sandbox;

public sealed class OpenSandboxOptions
{
    public const string SectionName = "OpenSandbox";

    public string BaseUrl { get; set; } = "http://localhost:8080";

    public string ApiKey { get; set; } = string.Empty;

    public string DefaultImage { get; set; } = "sandbox:latest";

    public int TimeoutSeconds { get; set; } = 30;
}
