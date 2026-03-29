namespace nem.Mimir.Application.Agents.Selection;

public interface ISkillQualityProvider
{
    Task<SkillQualityInfo?> GetQualityAsync(string agentName, CancellationToken ct = default);
}

public sealed record SkillQualityInfo(double SuccessRate, int TotalExecutions, TimeSpan AverageLatency);
