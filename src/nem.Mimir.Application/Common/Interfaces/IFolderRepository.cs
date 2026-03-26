using nem.Mimir.Domain.Entities;
using nem.Mimir.Domain.ValueObjects;

namespace nem.Mimir.Application.Common.Interfaces;

public interface IFolderRepository
{
    Task<Folder?> GetByIdAsync(FolderId id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Folder>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<Folder> CreateAsync(Folder folder, CancellationToken cancellationToken = default);

    Task UpdateAsync(Folder folder, CancellationToken cancellationToken = default);

    Task DeleteAsync(FolderId id, CancellationToken cancellationToken = default);
}
