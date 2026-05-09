namespace nem.Mimir.Application.Mcp;

public sealed class PersonaToolFilterOptions
{
    public Dictionary<string, PersonaConfig> Personas { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class PersonaConfig
{
    public string[] IncludedServerNames { get; init; } = [];

    public string[] IncludedToolNamePrefixes { get; init; } = [];

    public string[] ExcludedToolNames { get; init; } = [];
}
