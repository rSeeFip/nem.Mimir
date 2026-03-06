namespace Mimir.Domain.Entities;

using Mimir.Domain.Common;

public sealed class BackgroundTask : BaseEntity<Guid>
{
    public string TaskId { get; private set; } = string.Empty;
    public string AgentId { get; private set; } = string.Empty;
    public string Status { get; private set; } = string.Empty;
    public DateTimeOffset SubmittedAt { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public string? Result { get; private set; }
    public string? Error { get; private set; }
    public int Priority { get; private set; }

    private BackgroundTask()
    {
    }

    public static BackgroundTask Create(
        string taskId,
        string agentId,
        int priority,
        DateTimeOffset submittedAt)
    {
        if (string.IsNullOrWhiteSpace(taskId))
        {
            throw new ArgumentException("Task ID cannot be empty.", nameof(taskId));
        }

        if (string.IsNullOrWhiteSpace(agentId))
        {
            throw new ArgumentException("Agent ID cannot be empty.", nameof(agentId));
        }

        return new BackgroundTask
        {
            Id = Guid.NewGuid(),
            TaskId = taskId,
            AgentId = agentId,
            Status = "Queued",
            SubmittedAt = submittedAt,
            Priority = priority,
        };
    }

    public void MarkRunning(DateTimeOffset startedAt)
    {
        Status = "Running";
        StartedAt = startedAt;
        Error = null;
    }

    public void MarkCompleted(DateTimeOffset completedAt, string? result)
    {
        Status = "Completed";
        CompletedAt = completedAt;
        Result = result;
        Error = null;
    }

    public void MarkFailed(DateTimeOffset completedAt, string? error)
    {
        Status = "Failed";
        CompletedAt = completedAt;
        Error = error;
    }

    public void MarkCancelled(DateTimeOffset completedAt, string? error = null)
    {
        Status = "Cancelled";
        CompletedAt = completedAt;
        Error = error;
    }
}
