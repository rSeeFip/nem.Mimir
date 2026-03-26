namespace nem.Mimir.Domain.Entities;

using NoteTypedId = nem.Contracts.Identity.NoteId;
using nem.Mimir.Domain.Common;

public sealed class NoteVersion : BaseAuditableEntity<Guid>
{
    public NoteTypedId NoteId { get; private set; }
    public byte[] ContentSnapshot { get; private set; } = [];
    public string? ChangeDescription { get; private set; }
    public Guid CreatedByUserId { get; private set; }

    private NoteVersion() { }

    public static NoteVersion Create(
        NoteTypedId noteId,
        byte[] contentSnapshot,
        Guid createdByUserId,
        string? changeDescription = null)
    {
        if (noteId.IsEmpty)
            throw new ArgumentException("Note ID cannot be empty.", nameof(noteId));

        if (contentSnapshot is null || contentSnapshot.Length == 0)
            throw new ArgumentException("Content snapshot cannot be empty.", nameof(contentSnapshot));

        if (createdByUserId == Guid.Empty)
            throw new ArgumentException("Created-by user ID cannot be empty.", nameof(createdByUserId));

        return new NoteVersion
        {
            Id = Guid.NewGuid(),
            NoteId = noteId,
            ContentSnapshot = contentSnapshot.ToArray(),
            ChangeDescription = string.IsNullOrWhiteSpace(changeDescription) ? null : changeDescription.Trim(),
            CreatedByUserId = createdByUserId,
        };
    }
}
