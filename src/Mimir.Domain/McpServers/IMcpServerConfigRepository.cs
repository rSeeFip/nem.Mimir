namespace Mimir.Domain.McpServers;

public interface IMcpServerConfigRepository
{
    Task<McpServerConfig?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<McpServerConfig>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<McpServerConfig>> GetEnabledAsync(CancellationToken cancellationToken = default);

    Task AddAsync(McpServerConfig config, CancellationToken cancellationToken = default);

    Task UpdateAsync(McpServerConfig config, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
