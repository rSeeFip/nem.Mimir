namespace Mimir.Domain.Entities;

using Mimir.Domain.Common;

public sealed class SystemPrompt : BaseAuditableEntity<Guid>
{
    public string Name { get; private set; } = string.Empty;
    public string Template { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public bool IsDefault { get; private set; }
    public bool IsActive { get; private set; }

    private SystemPrompt() { }

    public static SystemPrompt Create(string name, string template, string description)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be empty.", nameof(name));

        if (string.IsNullOrWhiteSpace(template))
            throw new ArgumentException("Template cannot be empty.", nameof(template));

        return new SystemPrompt
        {
            Id = Guid.NewGuid(),
            Name = name,
            Template = template,
            Description = description ?? string.Empty,
            IsDefault = false,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    public void UpdateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be empty.", nameof(name));

        Name = name;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void UpdateTemplate(string template)
    {
        if (string.IsNullOrWhiteSpace(template))
            throw new ArgumentException("Template cannot be empty.", nameof(template));

        Template = template;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void UpdateDescription(string description)
    {
        Description = description ?? string.Empty;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SetAsDefault()
    {
        IsDefault = true;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void UnsetDefault()
    {
        IsDefault = false;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Activate()
    {
        IsActive = true;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
