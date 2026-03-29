using nem.Mimir.Application.Agents.Selection;

namespace nem.Mimir.Infrastructure.Agents;

public sealed class NoOpSkillQualityProvider : ISkillQualityProvider
{
    public Task<SkillQualityInfo?> GetQualityAsync(string agentName, CancellationToken ct = default)
    {
        return Task.FromResult<SkillQualityInfo?>(null);
    }
}
