namespace nem.Mimir.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Domain.Entities;
using nem.Mimir.Domain.ValueObjects;

internal sealed class ModelProfileRepository(MimirDbContext context) : IModelProfileRepository
{
    public async Task<ModelProfile?> GetByIdAsync(ModelProfileId id, CancellationToken cancellationToken = default)
    {
        return await context.ModelProfiles
            .FirstOrDefaultAsync(mp => mp.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ModelProfile>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var profiles = await context.ModelProfiles
            .AsNoTracking()
            .Where(mp => mp.UserId == userId)
            .OrderBy(mp => mp.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return profiles.AsReadOnly();
    }

    public async Task<ModelProfile?> GetByNameForUserAsync(Guid userId, string name, CancellationToken cancellationToken = default)
    {
        var normalizedName = name.Trim();
        return await context.ModelProfiles
            .FirstOrDefaultAsync(mp => mp.UserId == userId && mp.Name == normalizedName, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<ModelProfile> CreateAsync(ModelProfile profile, CancellationToken cancellationToken = default)
    {
        await context.ModelProfiles
            .AddAsync(profile, cancellationToken)
            .ConfigureAwait(false);

        return profile;
    }

    public Task UpdateAsync(ModelProfile profile, CancellationToken cancellationToken = default)
    {
        context.ModelProfiles.Update(profile);
        return Task.CompletedTask;
    }

    public async Task DeleteAsync(ModelProfileId id, Guid userId, CancellationToken cancellationToken = default)
    {
        var profile = await context.ModelProfiles
            .FirstOrDefaultAsync(mp => mp.Id == id && mp.UserId == userId, cancellationToken)
            .ConfigureAwait(false);

        if (profile is not null)
        {
            context.ModelProfiles.Remove(profile);
        }
    }
}
