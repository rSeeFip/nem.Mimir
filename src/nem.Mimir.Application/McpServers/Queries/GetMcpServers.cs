using MediatR;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.McpServers.Dtos;
using nem.Mimir.Domain.McpServers;

namespace nem.Mimir.Application.McpServers.Queries;

/// <summary>
/// Query to retrieve all MCP server configurations.
/// </summary>
public sealed record GetMcpServersQuery : IQuery<IReadOnlyList<McpServerDto>>;

internal sealed class GetMcpServersQueryHandler(
    IMcpServerConfigRepository repository) : IRequestHandler<GetMcpServersQuery, IReadOnlyList<McpServerDto>>
{
    public async Task<IReadOnlyList<McpServerDto>> Handle(GetMcpServersQuery request, CancellationToken cancellationToken)
    {
        var configs = await repository.GetAllAsync(cancellationToken);

        return configs.Select(MapToDto).ToList();
    }

    internal static McpServerDto MapToDto(McpServerConfig config) => new(
        Id: config.Id,
        Name: config.Name,
        TransportType: config.TransportType.ToString(),
        IsEnabled: config.IsEnabled,
        IsBundled: config.IsBundled,
        Description: config.Description,
        Command: config.Command,
        Arguments: config.Arguments,
        Url: config.Url,
        EnvironmentVariablesJson: config.EnvironmentVariablesJson,
        CreatedAt: config.CreatedAt,
        UpdatedAt: config.UpdatedAt,
        ToolWhitelists: config.ToolWhitelists
            .Select(w => new McpToolWhitelistDto(w.Id, w.ToolName, w.IsEnabled, w.CreatedAt))
            .ToList());
}
