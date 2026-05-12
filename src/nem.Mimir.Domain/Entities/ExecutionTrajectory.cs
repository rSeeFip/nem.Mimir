using nem.Contracts.Identity;
using nem.Mimir.Domain.Common;
using nem.Mimir.Domain.ValueObjects;

namespace nem.Mimir.Domain.Entities;

public sealed class ExecutionTrajectory : BaseAuditableEntity<TrajectoryId>
{
    private readonly List<TrajectoryStep> _steps = [];

    public string SessionId { get; private set; } = string.Empty;
    public string AgentId { get; private set; } = string.Empty;
    public DateTime StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public bool IsSuccess { get; private set; }
    public string? ErrorMessage { get; private set; }
    public int TotalSteps { get; private set; }
    public IReadOnlyList<TrajectoryStep> Steps => _steps.AsReadOnly();

    private ExecutionTrajectory() { }

    public static ExecutionTrajectory Create(string sessionId, string agentId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            throw new ArgumentException("SessionId cannot be empty.", nameof(sessionId));
        if (string.IsNullOrWhiteSpace(agentId))
            throw new ArgumentException("AgentId cannot be empty.", nameof(agentId));

        return new ExecutionTrajectory
        {
            Id = TrajectoryId.New(),
            SessionId = sessionId,
            AgentId = agentId,
            StartedAt = DateTime.UtcNow,
            IsSuccess = false
        };
    }

    public void AddStep(TrajectoryStep step)
    {
        ArgumentNullException.ThrowIfNull(step);
        if (CompletedAt.HasValue)
            throw new InvalidOperationException("Cannot add steps to a completed trajectory.");
        _steps.Add(step);
        TotalSteps = _steps.Count;
    }

    public void Complete()
    {
        if (CompletedAt.HasValue)
            throw new InvalidOperationException("Trajectory is already completed.");
        CompletedAt = DateTime.UtcNow;
        IsSuccess = true;
        TotalSteps = _steps.Count;
    }

    public void Fail(string errorMessage)
    {
        if (CompletedAt.HasValue)
            throw new InvalidOperationException("Trajectory is already completed.");
        if (string.IsNullOrWhiteSpace(errorMessage))
            throw new ArgumentException("Error message cannot be empty.", nameof(errorMessage));
        CompletedAt = DateTime.UtcNow;
        IsSuccess = false;
        ErrorMessage = errorMessage;
        TotalSteps = _steps.Count;
    }
}
