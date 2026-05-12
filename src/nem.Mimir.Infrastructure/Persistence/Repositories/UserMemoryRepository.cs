namespace nem.Mimir.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Domain.Entities;
using nem.Mimir.Domain.ValueObjects;

internal sealed class UserMemoryRepository(MimirDbContext context) : IUserMemoryRepository
{
    public async Task<UserMemory?> GetByIdAsync(UserMemoryId id, CancellationToken cancellationToken = default)
    {
        return await context.UserMemories
            .FirstOrDefaultAsync(um => um.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<UserMemory>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var memories = await context.UserMemories
            .AsNoTracking()
            .Where(um => um.UserId == userId)
            .OrderByDescending(um => um.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return memories.AsReadOnly();
    }

    public async Task<UserMemory> CreateAsync(UserMemory memory, CancellationToken cancellationToken = default)
    {
        await context.UserMemories
            .AddAsync(memory, cancellationToken)
            .ConfigureAwait(false);

        return memory;
    }

    public Task UpdateAsync(UserMemory memory, CancellationToken cancellationToken = default)
    {
        context.UserMemories.Update(memory);
        return Task.CompletedTask;
    }

    public async Task DeleteAsync(UserMemoryId id, Guid userId, CancellationToken cancellationToken = default)
    {
        var memory = await context.UserMemories
            .FirstOrDefaultAsync(um => um.Id == id && um.UserId == userId, cancellationToken)
            .ConfigureAwait(false);

        if (memory is not null)
        {
            context.UserMemories.Remove(memory);
        }
    }
}
