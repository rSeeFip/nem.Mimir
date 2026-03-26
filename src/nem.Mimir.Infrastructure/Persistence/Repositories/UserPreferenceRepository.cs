namespace nem.Mimir.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Domain.Entities;
using nem.Mimir.Domain.ValueObjects;

internal sealed class UserPreferenceRepository(MimirDbContext context) : IUserPreferenceRepository
{
    public async Task<UserPreference?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await context.UserPreferences
            .FirstOrDefaultAsync(up => up.UserId == userId, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<UserPreference?> GetByIdAsync(UserPreferenceId id, CancellationToken cancellationToken = default)
    {
        return await context.UserPreferences
            .FirstOrDefaultAsync(up => up.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<UserPreference> CreateAsync(UserPreference userPreference, CancellationToken cancellationToken = default)
    {
        await context.UserPreferences
            .AddAsync(userPreference, cancellationToken)
            .ConfigureAwait(false);

        return userPreference;
    }

    public Task UpdateAsync(UserPreference userPreference, CancellationToken cancellationToken = default)
    {
        context.UserPreferences.Update(userPreference);
        return Task.CompletedTask;
    }
}
