using nem.Mimir.Domain.Common;

namespace nem.Mimir.Domain.Plugins;

/// <summary>
/// Immutable value object describing a plugin's identity and metadata.
/// </summary>
public sealed record PluginMetadata
{
    public string Id { get; }
    public string Name { get; }
    public string Version { get; }
    public string Description { get; }

    private PluginMetadata(string id, string name, string version, string description)
    {
        Id = id;
        Name = name;
        Version = version;
        Description = description;
    }

    public static PluginMetadata Create(string id, string name, string version, string? description)
    {
        Guard.Against.NullOrWhiteSpace(id, nameof(id));
        Guard.Against.NullOrWhiteSpace(name, nameof(name));
        Guard.Against.NullOrWhiteSpace(version, nameof(version));

        return new PluginMetadata(id, name, version, description ?? string.Empty);
    }

    public override string ToString() => $"{Id} v{Version}";
}
