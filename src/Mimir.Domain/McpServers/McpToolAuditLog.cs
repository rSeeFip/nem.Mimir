namespace Mimir.Domain.McpServers;

public class McpToolAuditLog
{
    public Guid Id { get; set; }
    public Guid? McpServerConfigId { get; set; }
    public string ToolName { get; set; } = string.Empty;
    public string? Input { get; set; }
    public string? Output { get; set; }
    public long LatencyMs { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? UserId { get; set; }
    public Guid? ConversationId { get; set; }
    public DateTime Timestamp { get; set; }

    public McpServerConfig? McpServerConfig { get; set; }
}
