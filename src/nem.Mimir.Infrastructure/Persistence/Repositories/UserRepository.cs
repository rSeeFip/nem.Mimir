namespace nem.Mimir.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Domain.Entities;

internal sealed class UserRepository(MimirDbContext context) : IUserRepository
{
    public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<User> CreateAsync(User user, CancellationToken cancellationToken = default)
    {
        await context.Users
            .AddAsync(user, cancellationToken)
            .ConfigureAwait(false);

        return user;
    }

    public Task UpdateAsync(User user, CancellationToken cancellationToken = default)
    {
        context.Users.Update(user);
        return Task.CompletedTask;
    }
    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return await context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<(IReadOnlyList<User> Items, int TotalCount)> GetAllAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var totalCount = await context.Users
            .AsNoTracking()
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);

        var items = await context.Users
            .AsNoTracking()
            .OrderBy(u => u.Username)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return (items.AsReadOnly(), totalCount);
    }
}
