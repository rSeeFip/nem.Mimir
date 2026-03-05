using MediatR;
using Microsoft.Extensions.Logging;
using Mimir.Application.Common.Exceptions;
using Mimir.Application.Common.Interfaces;
using Mimir.Application.McpServers.Dtos;
using Mimir.Domain.McpServers;

namespace Mimir.Application.McpServers.Queries;

/// <summary>
/// Query to check the health of a connected MCP server.
/// </summary>
public sealed record GetMcpServerHealthQuery(Guid Id) : IQuery<McpServerHealthDto>;

internal sealed class GetMcpServerHealthQueryHandler(
    IMcpServerConfigRepository repository,
    IMcpClientManager clientManager,
    ILogger<GetMcpServerHealthQueryHandler> logger) : IRequestHandler<GetMcpServerHealthQuery, McpServerHealthDto>
{
    public async Task<McpServerHealthDto> Handle(GetMcpServerHealthQuery request, CancellationToken cancellationToken)
    {
        _ = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(McpServerConfig), request.Id);

        try
        {
            var isHealthy = await clientManager.HealthCheckAsync(request.Id, cancellationToken);
            return new McpServerHealthDto(request.Id, isHealthy, isHealthy ? null : "Health check returned unhealthy.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Health check failed for MCP server {ServerId}", request.Id);
            return new McpServerHealthDto(request.Id, false, ex.Message);
        }
    }
}
