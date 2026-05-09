using nem.Mimir.Domain.Entities;
using nem.Mimir.Domain.ValueObjects;

namespace nem.Mimir.Application.Common.Interfaces;

public interface IUserPreferenceRepository
{
    Task<UserPreference?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<UserPreference?> GetByIdAsync(UserPreferenceId id, CancellationToken cancellationToken = default);

    Task<UserPreference> CreateAsync(UserPreference userPreference, CancellationToken cancellationToken = default);

    Task UpdateAsync(UserPreference userPreference, CancellationToken cancellationToken = default);
}
