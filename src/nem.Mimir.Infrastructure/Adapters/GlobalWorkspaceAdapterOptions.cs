namespace nem.Mimir.Infrastructure.Adapters;

public sealed class GlobalWorkspaceAdapterOptions
{
    public const string SectionName = "GlobalWorkspaceAdapter";

    public HashSet<string> ParticipatingAgentServiceNames { get; } = new(StringComparer.OrdinalIgnoreCase);

    public bool IsParticipating(string serviceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);
        return ParticipatingAgentServiceNames.Contains(serviceName);
    }
}
