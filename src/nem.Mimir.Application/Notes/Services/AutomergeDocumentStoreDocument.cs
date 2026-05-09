namespace nem.Mimir.Application.Notes.Services;

public sealed class AutomergeDocumentStoreDocument
{
    public string Id { get; set; } = string.Empty;

    public string DocumentId { get; set; } = string.Empty;

    public byte[] State { get; set; } = [];

    public DateTimeOffset UpdatedAt { get; set; }
}
