namespace nem.Mimir.Application.Analysis;

using nem.Contracts.Events.Integration;
using TrajectoryId = global::nem.Contracts.Identity.TrajectoryId;

public interface ITrajectoryAnalyzer
{
    Task<EvolutionSuggestedEvent?> AnalyzeAsync(TrajectoryId trajectoryId, CancellationToken ct = default);
}
