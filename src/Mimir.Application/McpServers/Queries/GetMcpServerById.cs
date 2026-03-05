using MediatR;
using Mimir.Application.Common.Exceptions;
using Mimir.Application.Common.Interfaces;
using Mimir.Application.McpServers.Dtos;
using Mimir.Domain.McpServers;

namespace Mimir.Application.McpServers.Queries;

/// <summary>
/// Query to retrieve a single MCP server configuration by its identifier.
/// </summary>
public sealed record GetMcpServerByIdQuery(Guid Id) : IQuery<McpServerDto>;

internal sealed class GetMcpServerByIdQueryHandler(
    IMcpServerConfigRepository repository) : IRequestHandler<GetMcpServerByIdQuery, McpServerDto>
{
    public async Task<McpServerDto> Handle(GetMcpServerByIdQuery request, CancellationToken cancellationToken)
    {
        var config = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(McpServerConfig), request.Id);

        return GetMcpServersQueryHandler.MapToDto(config);
    }
}
