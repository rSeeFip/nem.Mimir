namespace nem.Mimir.Domain.McpServers;

public class McpToolWhitelist
{
    public Guid Id { get; set; }
    public Guid McpServerConfigId { get; set; }
    public required string ToolName { get; set; }
    public bool IsEnabled { get; set; } = true;
    public DateTime CreatedAt { get; set; }

    public McpServerConfig McpServerConfig { get; set; } = null!;
}
