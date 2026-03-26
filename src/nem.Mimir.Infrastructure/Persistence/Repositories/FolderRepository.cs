namespace nem.Mimir.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Domain.Entities;
using nem.Mimir.Domain.ValueObjects;

internal sealed class FolderRepository(MimirDbContext context) : IFolderRepository
{
    public async Task<Folder?> GetByIdAsync(FolderId id, CancellationToken cancellationToken = default)
    {
        return await context.Folders
            .FirstOrDefaultAsync(f => f.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Folder>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var folders = await context.Folders
            .AsNoTracking()
            .Where(f => f.UserId == userId)
            .OrderBy(f => f.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return folders.AsReadOnly();
    }

    public async Task<Folder> CreateAsync(Folder folder, CancellationToken cancellationToken = default)
    {
        await context.Folders
            .AddAsync(folder, cancellationToken)
            .ConfigureAwait(false);

        return folder;
    }

    public Task UpdateAsync(Folder folder, CancellationToken cancellationToken = default)
    {
        context.Folders.Update(folder);
        return Task.CompletedTask;
    }

    public async Task DeleteAsync(FolderId id, CancellationToken cancellationToken = default)
    {
        var folder = await context.Folders
            .FirstOrDefaultAsync(f => f.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (folder is not null)
        {
            context.Folders.Remove(folder);
        }
    }
}
