namespace nem.Mimir.Domain.Entities;

using nem.Mimir.Domain.Common;
using nem.Mimir.Domain.ValueObjects;

public sealed class Folder : BaseAuditableEntity<FolderId>
{
    public string Name { get; private set; } = string.Empty;
    public Guid UserId { get; private set; }
    public FolderId? ParentId { get; private set; }
    public bool IsExpanded { get; private set; }

    private Folder() { }

    public static Folder Create(Guid userId, string name, FolderId? parentId = null)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("User ID cannot be empty.", nameof(userId));

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Folder name cannot be empty.", nameof(name));

        return new Folder
        {
            Id = FolderId.New(),
            UserId = userId,
            Name = name.Trim(),
            ParentId = parentId,
            IsExpanded = false,
        };
    }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Folder name cannot be empty.", nameof(name));

        Name = name.Trim();
    }

    public void SetParent(FolderId? parentId)
    {
        ParentId = parentId;
    }

    public void ToggleExpanded()
    {
        IsExpanded = !IsExpanded;
    }
}
