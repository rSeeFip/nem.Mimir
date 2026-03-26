using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Domain.Entities;
using NoteTypedId = nem.Contracts.Identity.NoteId;

namespace nem.Mimir.Application.Notes.Services;

public sealed class YjsDocumentStore
{
    private readonly INoteRepository _noteRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;

    public YjsDocumentStore(
        INoteRepository noteRepository,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork)
    {
        _noteRepository = noteRepository;
        _currentUserService = currentUserService;
        _unitOfWork = unitOfWork;
    }

    public async Task<byte[]> GetDocumentStateAsync(NoteTypedId noteId, CancellationToken cancellationToken = default)
    {
        var note = await _noteRepository.GetByIdAsync(noteId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(Note), noteId.Value);

        return note.Content.ToArray();
    }

    public async Task<byte[]> ApplyUpdateAsync(NoteTypedId noteId, byte[] update, CancellationToken cancellationToken = default)
    {
        if (update is null)
            throw new ArgumentNullException(nameof(update));

        var note = await _noteRepository.GetWithCollaboratorsAsync(noteId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(Note), noteId.Value);

        var userGuid = ResolveCurrentUserId();
        if (!note.CanEdit(userGuid))
            throw new ForbiddenAccessException();

        var current = note.Content;
        var merged = new byte[current.Length + update.Length];
        Buffer.BlockCopy(current, 0, merged, 0, current.Length);
        Buffer.BlockCopy(update, 0, merged, current.Length, update.Length);

        note.Update(userGuid, note.Title, merged, "Applied incremental Yjs update");

        await _noteRepository.UpdateAsync(note, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return merged;
    }

    public Task<byte[]> GetFullDocumentAsync(NoteTypedId noteId, CancellationToken cancellationToken = default)
    {
        return GetDocumentStateAsync(noteId, cancellationToken);
    }

    private Guid ResolveCurrentUserId()
    {
        var userId = _currentUserService.UserId
            ?? throw new ForbiddenAccessException("User is not authenticated.");

        if (!Guid.TryParse(userId, out var userGuid))
            throw new ForbiddenAccessException("User identity could not be determined.");

        return userGuid;
    }
}
