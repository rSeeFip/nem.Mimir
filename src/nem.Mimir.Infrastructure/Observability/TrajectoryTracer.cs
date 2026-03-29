using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace nem.Mimir.Infrastructure.Observability;

public static class TrajectoryTracer
{
    public static readonly ActivitySource ActivitySource = new("nem.Mimir.SelfImprovement", "1.0.0");

    private static readonly Meter Meter = new("nem.Mimir.SelfImprovement", "1.0.0");

    public static readonly Counter<long> TrajectoriesRecordedTotal =
        Meter.CreateCounter<long>("mimir.trajectories.recorded_total", "trajectories", "Total trajectories recorded");

    public static readonly Counter<long> TrajectoriesAnalyzedTotal =
        Meter.CreateCounter<long>("mimir.trajectories.analyzed_total", "trajectories", "Total trajectories analyzed");

    public static readonly Counter<long> EvolutionSuggestionsTotal =
        Meter.CreateCounter<long>("mimir.evolution.suggestions_total", "suggestions", "Total evolution suggestions emitted");

    public static readonly Histogram<double> AnalysisLatencyMs =
        Meter.CreateHistogram<double>("mimir.analysis.latency_ms", "ms", "Trajectory analysis LLM call latency");
}
