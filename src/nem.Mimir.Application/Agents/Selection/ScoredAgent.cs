namespace nem.Mimir.Application.Agents.Selection;

public sealed record ScoredAgent(
    global::nem.Mimir.Application.Agents.ISpecialistAgent Agent,
    double Score,
    Dictionary<string, double> StepScores)
{
    public ScoredAgent AddStepScore(string stepName, double rawStepScore, double weight = 1d)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stepName);

        var weightedScore = rawStepScore * weight;

        var updatedScores = new Dictionary<string, double>(StepScores, StringComparer.OrdinalIgnoreCase)
        {
            [stepName] = weightedScore,
        };

        return this with
        {
            Score = Score + weightedScore,
            StepScores = updatedScores,
        };
    }

    public static ScoredAgent Create(global::nem.Mimir.Application.Agents.ISpecialistAgent agent)
    {
        ArgumentNullException.ThrowIfNull(agent);
        return new ScoredAgent(agent, 0, new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase));
    }
}
