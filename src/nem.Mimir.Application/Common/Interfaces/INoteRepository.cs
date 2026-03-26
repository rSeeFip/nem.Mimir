using nem.Mimir.Application.Common.Models;
using nem.Mimir.Domain.Entities;
using NoteTypedId = nem.Contracts.Identity.NoteId;

namespace nem.Mimir.Application.Common.Interfaces;

public interface INoteRepository
{
    Task<Note?> GetByIdAsync(NoteTypedId id, CancellationToken cancellationToken = default);

    Task<Note?> GetWithCollaboratorsAsync(NoteTypedId id, CancellationToken cancellationToken = default);

    Task<Note?> GetWithVersionsAsync(NoteTypedId id, CancellationToken cancellationToken = default);

    Task<PaginatedList<Note>> GetByUserIdAsync(Guid userId, int pageNumber, int pageSize, CancellationToken cancellationToken = default);

    Task<Note> CreateAsync(Note note, CancellationToken cancellationToken = default);

    Task UpdateAsync(Note note, CancellationToken cancellationToken = default);

    Task DeleteAsync(NoteTypedId id, CancellationToken cancellationToken = default);

    Task<PaginatedList<NoteVersion>> GetVersionsAsync(NoteTypedId noteId, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
}
