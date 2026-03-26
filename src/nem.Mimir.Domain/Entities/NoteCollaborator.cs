namespace nem.Mimir.Domain.Entities;

using NoteTypedId = nem.Contracts.Identity.NoteId;
using nem.Mimir.Domain.Common;
using nem.Mimir.Domain.Enums;

public sealed class NoteCollaborator : BaseAuditableEntity<Guid>
{
    public NoteTypedId NoteId { get; private set; }
    public Guid UserId { get; private set; }
    public NotePermission Permission { get; private set; }

    private NoteCollaborator() { }

    public static NoteCollaborator Create(NoteTypedId noteId, Guid userId, NotePermission permission)
    {
        if (noteId.IsEmpty)
            throw new ArgumentException("Note ID cannot be empty.", nameof(noteId));

        if (userId == Guid.Empty)
            throw new ArgumentException("User ID cannot be empty.", nameof(userId));

        return new NoteCollaborator
        {
            Id = Guid.NewGuid(),
            NoteId = noteId,
            UserId = userId,
            Permission = permission,
        };
    }

    public void UpdatePermission(NotePermission permission)
    {
        Permission = permission;
    }
}
