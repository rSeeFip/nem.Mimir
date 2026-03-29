namespace nem.Mimir.Domain.ValueObjects;

public sealed class TrajectoryStep
{
    public string Action { get; private set; } = string.Empty;
    public string? Input { get; private set; }
    public string? Output { get; private set; }
    public TimeSpan Duration { get; private set; }
    public bool IsSuccess { get; private set; }
    public DateTime OccurredAt { get; private set; }

    private TrajectoryStep() { }

    public static TrajectoryStep Create(string action, string? input, string? output, TimeSpan duration, bool isSuccess)
    {
        if (string.IsNullOrWhiteSpace(action))
            throw new ArgumentException("Action cannot be empty.", nameof(action));

        return new TrajectoryStep
        {
            Action = action,
            Input = input,
            Output = output,
            Duration = duration,
            IsSuccess = isSuccess,
            OccurredAt = DateTime.UtcNow
        };
    }

    public static TrajectoryStep Create(string action, string? input, string? output, TimeSpan duration, bool isSuccess, DateTime occurredAt)
    {
        var step = Create(action, input, output, duration, isSuccess);
        step.OccurredAt = occurredAt;
        return step;
    }
}
