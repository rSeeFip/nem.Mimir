using System.Diagnostics;
using Wolverine;
using nem.Contracts.Events.Integration;
using nem.Mimir.Infrastructure.Observability;

namespace nem.Mimir.Application.Analysis;

public static class TrajectoryAnalyzedConsumer
{
    public static async Task Handle(
        TrajectoryRecordedEvent @event,
        ITrajectoryAnalyzer analyzer,
        IMessageBus bus,
        CancellationToken ct)
    {
        using var activity = TrajectoryTracer.ActivitySource.StartActivity("trajectory.analyze", ActivityKind.Consumer);
        activity?.SetTag("trajectory.id", @event.TrajectoryId.ToString());
        activity?.SetTag("skill.id", @event.SkillId.ToString());
        activity?.SetTag("skill.version_id", @event.SkillVersionId.ToString());

        TrajectoryTracer.TrajectoriesAnalyzedTotal.Add(1);

        // Only analyze failed trajectories (where Succeeded is false)
        if (@event.Succeeded)
        {
            return;
        }

        var result = await analyzer.AnalyzeAsync(@event.TrajectoryId, ct);

        if (result is not null)
        {
            TrajectoryTracer.EvolutionSuggestionsTotal.Add(1);
            await bus.PublishAsync(result);
        }
    }
}
