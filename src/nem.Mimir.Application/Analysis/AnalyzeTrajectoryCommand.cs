namespace nem.Mimir.Application.Analysis;

using TrajectoryId = global::nem.Contracts.Identity.TrajectoryId;

public sealed record AnalyzeTrajectoryCommand(TrajectoryId TrajectoryId);
