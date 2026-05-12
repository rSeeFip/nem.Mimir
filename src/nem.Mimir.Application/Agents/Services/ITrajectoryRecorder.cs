namespace nem.Mimir.Application.Agents.Services;

using nem.Contracts.Identity;
using nem.Mimir.Domain.ValueObjects;
using TrajectoryId = nem.Contracts.Identity.TrajectoryId;

public interface ITrajectoryRecorder
{
    Task<TrajectoryId> StartRecordingAsync(string sessionId, string agentId, CancellationToken ct = default);

    Task RecordStepAsync(TrajectoryId trajectoryId, TrajectoryStep step, CancellationToken ct = default);

    Task CompleteRecordingAsync(
        TrajectoryId trajectoryId,
        bool isSuccess,
        string? errorMessage = null,
        SkillId? skillId = null,
        SkillVersionId? skillVersionId = null,
        CancellationToken ct = default);
}
