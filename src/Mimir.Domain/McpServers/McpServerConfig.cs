namespace Mimir.Domain.McpServers;

public class McpServerConfig
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public McpTransportType TransportType { get; set; }

    public string? Command { get; set; }
    public string? Arguments { get; set; }

    public string? Url { get; set; }

    public string? EnvironmentVariablesJson { get; set; }
    public bool IsEnabled { get; set; }
    public bool IsBundled { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<McpToolWhitelist> ToolWhitelists { get; set; } = new List<McpToolWhitelist>();
}
