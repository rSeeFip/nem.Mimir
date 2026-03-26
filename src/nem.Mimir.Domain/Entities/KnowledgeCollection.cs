namespace nem.Mimir.Domain.Entities;

using nem.Mimir.Domain.Common;
using nem.Mimir.Domain.ValueObjects;

public sealed class KnowledgeCollection : BaseAuditableEntity<KnowledgeCollectionId>
{
    public Guid UserId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;

    private readonly List<KnowledgeDocument> _documents = [];
    public IReadOnlyCollection<KnowledgeDocument> Documents => _documents.AsReadOnly();

    private KnowledgeCollection() { }

    public static KnowledgeCollection Create(Guid userId, string name, string? description = null)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("User ID cannot be empty.", nameof(userId));

        var normalizedName = ValidateName(name);

        return new KnowledgeCollection
        {
            Id = KnowledgeCollectionId.New(),
            UserId = userId,
            Name = normalizedName,
            Description = description?.Trim() ?? string.Empty,
        };
    }

    public void Update(string name, string? description)
    {
        Name = ValidateName(name);
        Description = description?.Trim() ?? string.Empty;
    }

    public void AddDocument(Guid documentId, string fileName, string storageUrl, string? contentType = null)
    {
        if (documentId == Guid.Empty)
            throw new ArgumentException("Document ID cannot be empty.", nameof(documentId));

        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("File name cannot be empty.", nameof(fileName));

        if (string.IsNullOrWhiteSpace(storageUrl))
            throw new ArgumentException("Storage URL cannot be empty.", nameof(storageUrl));

        if (_documents.Any(d => d.DocumentId == documentId))
        {
            return;
        }

        _documents.Add(new KnowledgeDocument(
            documentId,
            fileName.Trim(),
            storageUrl.Trim(),
            contentType?.Trim()));
    }

    public bool RemoveDocument(Guid documentId)
    {
        var existing = _documents.FirstOrDefault(d => d.DocumentId == documentId);
        if (existing is null)
        {
            return false;
        }

        _documents.Remove(existing);
        return true;
    }

    private static string ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be empty.", nameof(name));

        return name.Trim();
    }
}

public sealed record KnowledgeDocument(
    Guid DocumentId,
    string FileName,
    string StorageUrl,
    string? ContentType,
    DateTimeOffset AddedAt)
{
    public KnowledgeDocument(Guid documentId, string fileName, string storageUrl, string? contentType)
        : this(documentId, fileName, storageUrl, contentType, DateTimeOffset.UtcNow)
    {
    }
}
