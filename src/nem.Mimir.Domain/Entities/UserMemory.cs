namespace nem.Mimir.Domain.Entities;

using nem.Mimir.Domain.Common;
using nem.Mimir.Domain.ValueObjects;

public sealed class UserMemory : BaseAuditableEntity<UserMemoryId>
{
    public Guid UserId { get; private set; }
    public string Content { get; private set; } = string.Empty;
    public string? Context { get; private set; }
    public string Source { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }

    private UserMemory() { }

    public static UserMemory Create(Guid userId, string content, string? context = null)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("User ID cannot be empty.", nameof(userId));

        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Content cannot be empty.", nameof(content));

        return new UserMemory
        {
            Id = UserMemoryId.New(),
            UserId = userId,
            Content = content.Trim(),
            Context = NormalizeContext(context),
            Source = "manual",
            IsActive = true,
        };
    }

    public void Update(string content, string? context)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Content cannot be empty.", nameof(content));

        Content = content.Trim();
        Context = NormalizeContext(context);
    }

    public void MarkAsActive()
    {
        IsActive = true;
    }

    public void MarkAsInactive()
    {
        IsActive = false;
    }

    private static string? NormalizeContext(string? context) =>
        string.IsNullOrWhiteSpace(context) ? null : context.Trim();
}
