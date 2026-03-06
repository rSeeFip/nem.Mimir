namespace Mimir.Application.Tasks;

public sealed class BackgroundTaskOptions
{
    public const string SectionName = "BackgroundTasks";

    public int MaxConcurrency { get; set; } = 5;

    public int DefaultTimeoutSeconds { get; set; } = 300;

    public int ResultCacheMinutes { get; set; } = 60;
}
