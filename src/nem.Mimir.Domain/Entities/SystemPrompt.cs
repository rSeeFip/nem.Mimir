namespace nem.Mimir.Domain.Entities;

using nem.Mimir.Domain.Common;

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
        };
    }

    public void UpdateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be empty.", nameof(name));

        Name = name;
    }

    public void UpdateTemplate(string template)
    {
        if (string.IsNullOrWhiteSpace(template))
            throw new ArgumentException("Template cannot be empty.", nameof(template));

        Template = template;
    }

    public void UpdateDescription(string description)
    {
        Description = description ?? string.Empty;
    }

    public void SetAsDefault()
    {
        IsDefault = true;
    }

    public void UnsetDefault()
    {
        IsDefault = false;
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public void Activate()
    {
        IsActive = true;
    }
}
