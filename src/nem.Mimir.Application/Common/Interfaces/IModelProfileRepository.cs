using nem.Mimir.Domain.Entities;
using nem.Mimir.Domain.ValueObjects;

namespace nem.Mimir.Application.Common.Interfaces;

public interface IModelProfileRepository
{
    Task<ModelProfile?> GetByIdAsync(ModelProfileId id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ModelProfile>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<ModelProfile?> GetByNameForUserAsync(Guid userId, string name, CancellationToken cancellationToken = default);

    Task<ModelProfile> CreateAsync(ModelProfile profile, CancellationToken cancellationToken = default);

    Task UpdateAsync(ModelProfile profile, CancellationToken cancellationToken = default);

    Task DeleteAsync(ModelProfileId id, Guid userId, CancellationToken cancellationToken = default);
}
