namespace nem.Mimir.Domain.Entities;

using NoteTypedId = nem.Contracts.Identity.NoteId;
using nem.Mimir.Domain.Common;
using nem.Mimir.Domain.Enums;
using nem.Mimir.Domain.Events;

public sealed class Note : BaseAuditableEntity<NoteTypedId>
{
    public Guid OwnerId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public byte[] Content { get; private set; } = [];

    private readonly List<string> _tags = [];
    public IReadOnlyCollection<string> Tags => _tags.AsReadOnly();

    private readonly List<NoteCollaborator> _collaborators = [];
    public IReadOnlyCollection<NoteCollaborator> Collaborators => _collaborators.AsReadOnly();

    private readonly List<NoteVersion> _versions = [];
    public IReadOnlyCollection<NoteVersion> Versions => _versions.AsReadOnly();

    private Note() { }

    public static Note Create(Guid ownerId, string title, byte[] content, IEnumerable<string>? tags = null)
    {
        if (ownerId == Guid.Empty)
            throw new ArgumentException("Owner ID cannot be empty.", nameof(ownerId));

        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title cannot be empty.", nameof(title));

        if (title.Trim().Length > 200)
            throw new ArgumentException("Title must not exceed 200 characters.", nameof(title));

        if (content is null)
            throw new ArgumentNullException(nameof(content));

        var note = new Note
        {
            Id = NoteTypedId.New(),
            OwnerId = ownerId,
            Title = title.Trim(),
            Content = content.ToArray(),
        };

        note.AddCollaborator(ownerId, NotePermission.Owner);

        if (tags is not null)
        {
            foreach (var tag in tags)
            {
                note.AddTag(tag);
            }
        }

        note.AddDomainEvent(new NoteCreatedEvent(note.Id, ownerId));
        return note;
    }

    public void Update(Guid updatedByUserId, string title, byte[] content, string? changeDescription = null)
    {
        EnsureCanEdit(updatedByUserId);

        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title cannot be empty.", nameof(title));

        if (title.Trim().Length > 200)
            throw new ArgumentException("Title must not exceed 200 characters.", nameof(title));

        if (content is null)
            throw new ArgumentNullException(nameof(content));

        var previousSnapshot = Content.ToArray();
        if (previousSnapshot.Length > 0)
        {
            _versions.Add(NoteVersion.Create(Id, previousSnapshot, updatedByUserId, changeDescription));
        }

        Title = title.Trim();
        Content = content.ToArray();

        AddDomainEvent(new NoteUpdatedEvent(Id, updatedByUserId));
    }

    public void AddTag(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
            return;

        var normalizedTag = tag.Trim();
        if (_tags.Any(existing => string.Equals(existing, normalizedTag, StringComparison.OrdinalIgnoreCase)))
            return;

        _tags.Add(normalizedTag);
    }

    public void RemoveTag(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
            return;

        var existing = _tags.FirstOrDefault(value => string.Equals(value, tag.Trim(), StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            _tags.Remove(existing);
        }
    }

    public void AddCollaborator(Guid userId, NotePermission permission)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("User ID cannot be empty.", nameof(userId));

        var existing = _collaborators.FirstOrDefault(collaborator => collaborator.UserId == userId);
        if (existing is not null)
        {
            existing.UpdatePermission(permission);
            return;
        }

        _collaborators.Add(NoteCollaborator.Create(Id, userId, permission));
        AddDomainEvent(new CollaboratorAddedEvent(Id, userId, permission));
    }

    public void RemoveCollaborator(Guid userId)
    {
        if (userId == OwnerId)
            throw new InvalidOperationException("Owner cannot be removed as collaborator.");

        var existing = _collaborators.FirstOrDefault(collaborator => collaborator.UserId == userId);
        if (existing is not null)
        {
            _collaborators.Remove(existing);
        }
    }

    public void RestoreVersion(Guid actorUserId, Guid versionId)
    {
        EnsureCanEdit(actorUserId);

        var version = _versions.FirstOrDefault(item => item.Id == versionId)
            ?? throw new InvalidOperationException("Version not found.");

        var previousSnapshot = Content.ToArray();
        if (previousSnapshot.Length > 0)
        {
            _versions.Add(NoteVersion.Create(Id, previousSnapshot, actorUserId, "Restore previous version"));
        }

        Content = version.ContentSnapshot.ToArray();
        AddDomainEvent(new NoteUpdatedEvent(Id, actorUserId));
    }

    public bool CanView(Guid userId)
    {
        if (userId == OwnerId)
            return true;

        return _collaborators.Any(collaborator => collaborator.UserId == userId);
    }

    public bool CanEdit(Guid userId)
    {
        if (userId == OwnerId)
            return true;

        var collaborator = _collaborators.FirstOrDefault(item => item.UserId == userId);
        return collaborator is not null && collaborator.Permission is NotePermission.Owner or NotePermission.Editor;
    }

    private void EnsureCanEdit(Guid userId)
    {
        if (!CanEdit(userId))
        {
            throw new InvalidOperationException("User does not have permission to edit this note.");
        }
    }
}
