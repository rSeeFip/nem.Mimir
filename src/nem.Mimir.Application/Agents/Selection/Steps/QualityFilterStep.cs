using Microsoft.Extensions.Logging;

namespace nem.Mimir.Application.Agents.Selection.Steps;

public sealed class QualityFilterStep : ISelectionStep
{
    private readonly ISkillQualityProvider _qualityProvider;
    private readonly ILogger<QualityFilterStep> _logger;

    public QualityFilterStep(
        ISkillQualityProvider qualityProvider,
        ILogger<QualityFilterStep> logger)
    {
        _qualityProvider = qualityProvider;
        _logger = logger;
    }

    public string Name => SelectionStepNames.Quality;

    public async Task<SelectionContext> ExecuteAsync(SelectionContext context, CancellationToken ct = default)
    {
        if (context.Candidates.Count == 0)
        {
            return context;
        }

        var stepDefinition = context.ProcessDefinition.GetStep(Name);
        var successRateThreshold = stepDefinition.SuccessRateThreshold ?? 0.5d;

        var enrichedCandidates = new List<(ScoredAgent Candidate, SkillQualityInfo? Quality, bool HasQuality, bool IsDegraded)>();

        foreach (var candidate in context.Candidates)
        {
            ct.ThrowIfCancellationRequested();

            var quality = await _qualityProvider.GetQualityAsync(candidate.Agent.Name, ct).ConfigureAwait(false);
            var hasQuality = quality is not null;
            var successRate = quality?.SuccessRate ?? 1.0;
            var isDegraded = hasQuality && successRate < successRateThreshold;

            enrichedCandidates.Add((
                candidate.AddStepScore(Name, successRate, stepDefinition.Weight),
                quality,
                hasQuality,
                isDegraded));
        }

        if (enrichedCandidates.TrueForAll(x => !x.HasQuality))
        {
            _logger.LogDebug("QualityFilterStep skipped filtering because no quality data was available for any candidate.");
            return context.WithCandidates(enrichedCandidates.Select(x => x.Candidate).ToList());
        }

        var hasNonDegradedAlternative = enrichedCandidates.Exists(x => !x.IsDegraded);
        if (!hasNonDegradedAlternative)
        {
            _logger.LogDebug("QualityFilterStep kept all {Count} candidates because all with quality data were degraded.", enrichedCandidates.Count);
            return context.WithCandidates(enrichedCandidates.Select(x => x.Candidate).ToList());
        }

        var filtered = enrichedCandidates
            .Where(x => !x.IsDegraded)
            .Select(x => x.Candidate)
            .ToList();

        _logger.LogDebug(
            "QualityFilterStep filtered candidates from {OriginalCount} to {FilteredCount} using success-rate threshold {Threshold}.",
            context.Candidates.Count,
            filtered.Count,
            successRateThreshold);

        return context.WithCandidates(filtered);
    }
}
