namespace nem.Mimir.Domain.McpServers;

public interface IToolWhitelistService
{
    Task<bool> IsToolAllowedAsync(Guid mcpServerConfigId, string toolName, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<McpToolWhitelist>> GetAllowedToolsAsync(Guid mcpServerConfigId, CancellationToken cancellationToken = default);

    Task SetToolWhitelistAsync(Guid mcpServerConfigId, string toolName, bool isEnabled, CancellationToken cancellationToken = default);

    bool ValidateArguments(string argumentsJson);
}
