namespace nem.Mimir.Infrastructure.Persistence;

using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using nem.Contracts.Events.Integration;
using nem.Contracts.Identity;
using nem.Mimir.Application.Agents.Services;
using nem.Mimir.Domain.Entities;
using nem.Mimir.Domain.ValueObjects;
using nem.Mimir.Infrastructure.Observability;
using Wolverine;

internal sealed class TrajectoryRecorder(MimirDbContext dbContext, IMessageBus messageBus) : ITrajectoryRecorder
{
    public async Task<TrajectoryId> StartRecordingAsync(string sessionId, string agentId, CancellationToken ct = default)
    {
        using var activity = TrajectoryTracer.ActivitySource.StartActivity("trajectory.record", ActivityKind.Internal);
        activity?.SetTag("session.id", sessionId);
        activity?.SetTag("agent.id", agentId);

        var trajectory = ExecutionTrajectory.Create(sessionId, agentId);
        activity?.SetTag("trajectory.id", trajectory.Id.ToString());

        await dbContext.ExecutionTrajectories
            .AddAsync(trajectory, ct)
            .ConfigureAwait(false);

        await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);

        return trajectory.Id;
    }

    public async Task RecordStepAsync(TrajectoryId trajectoryId, TrajectoryStep step, CancellationToken ct = default)
    {
        var trajectory = await dbContext.ExecutionTrajectories
            .FirstOrDefaultAsync(t => t.Id == trajectoryId, ct)
            .ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Execution trajectory '{trajectoryId}' was not found.");

        trajectory.AddStep(step);
        await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task CompleteRecordingAsync(
        TrajectoryId trajectoryId,
        bool isSuccess,
        string? errorMessage = null,
        SkillId? skillId = null,
        SkillVersionId? skillVersionId = null,
        CancellationToken ct = default)
    {
        using var activity = TrajectoryTracer.ActivitySource.StartActivity("trajectory.record.complete", ActivityKind.Internal);
        activity?.SetTag("trajectory.id", trajectoryId.ToString());
        activity?.SetTag("skill.id", (skillId ?? SkillId.Empty).ToString());
        activity?.SetTag("skill.version_id", (skillVersionId ?? SkillVersionId.Empty).ToString());
        activity?.SetTag("trajectory.success", isSuccess);

        var trajectory = await dbContext.ExecutionTrajectories
            .FirstOrDefaultAsync(t => t.Id == trajectoryId, ct)
            .ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Execution trajectory '{trajectoryId}' was not found.");

        if (isSuccess)
        {
            trajectory.Complete();
        }
        else
        {
            trajectory.Fail(errorMessage ?? "Agent execution failed.");
        }

        await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);

        TrajectoryTracer.TrajectoriesRecordedTotal.Add(1);

        await messageBus.PublishAsync(new TrajectoryRecordedEvent(
                trajectory.Id,
                skillId ?? SkillId.Empty,
                skillVersionId ?? SkillVersionId.Empty,
                isSuccess,
                DateTimeOffset.UtcNow))
            .ConfigureAwait(false);
    }
}
