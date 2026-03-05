using MediatR;
using Mimir.Application.Common.Interfaces;
using Mimir.Application.McpServers.Dtos;

namespace Mimir.Application.McpServers.Queries;

/// <summary>
/// Query to retrieve MCP tool audit logs, optionally filtered by server ID.
/// </summary>
public sealed record GetMcpAuditLogsQuery(
    Guid? ServerId,
    int PageNumber = 1,
    int PageSize = 20) : IQuery<IReadOnlyList<McpAuditLogDto>>;

internal sealed class GetMcpAuditLogsQueryHandler
    : IRequestHandler<GetMcpAuditLogsQuery, IReadOnlyList<McpAuditLogDto>>
{
    // TODO: Inject MimirDbContext or a dedicated audit log repository when available.
    // The McpToolAuditLogs DbSet exists in MimirDbContext but we avoid a direct
    // Infrastructure dependency from Application layer. A repository or read-model
    // interface should be introduced in a follow-up task.
    public Task<IReadOnlyList<McpAuditLogDto>> Handle(
        GetMcpAuditLogsQuery request,
        CancellationToken cancellationToken)
    {
        // Placeholder — returns an empty list until audit log read-model is wired.
        IReadOnlyList<McpAuditLogDto> result = [];
        return Task.FromResult(result);
    }
}
