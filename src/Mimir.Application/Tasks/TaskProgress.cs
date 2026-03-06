namespace Mimir.Application.Tasks;

public sealed record TaskProgress(
    string TaskId,
    int PercentComplete,
    string StatusMessage,
    DateTimeOffset Timestamp);
