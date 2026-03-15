namespace nem.Mimir.Domain.McpServers;

public interface IToolAuditLogger
{
    Task LogToolExecutionAsync(McpToolAuditLog entry, CancellationToken ct = default);
}
