namespace nem.Mimir.Infrastructure.Mcp;

public sealed class McpServerConfiguration
{
    public string Name { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    public string TransportType { get; set; } = "sse";

    public bool RequiresAuth { get; set; } = true;

    public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(30);

    public TimeSpan ToolSchemasCacheTtl { get; set; } = TimeSpan.FromMinutes(5);
}
