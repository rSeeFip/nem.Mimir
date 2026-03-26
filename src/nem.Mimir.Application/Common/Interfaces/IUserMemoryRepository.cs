using nem.Mimir.Domain.Entities;
using nem.Mimir.Domain.ValueObjects;

namespace nem.Mimir.Application.Common.Interfaces;

public interface IUserMemoryRepository
{
    Task<UserMemory?> GetByIdAsync(UserMemoryId id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserMemory>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<UserMemory> CreateAsync(UserMemory memory, CancellationToken cancellationToken = default);

    Task UpdateAsync(UserMemory memory, CancellationToken cancellationToken = default);

    Task DeleteAsync(UserMemoryId id, Guid userId, CancellationToken cancellationToken = default);
}
