using nem.Mimir.Domain.Entities;

namespace nem.Mimir.Application.Common.Interfaces;

public interface IArenaConfigRepository
{
    Task<ArenaConfig?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<ArenaConfig> CreateAsync(ArenaConfig config, CancellationToken cancellationToken = default);

    Task UpdateAsync(ArenaConfig config, CancellationToken cancellationToken = default);
}
