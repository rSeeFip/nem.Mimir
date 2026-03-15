namespace nem.Mimir.Application.Services.Memory;

public sealed class WorkingMemoryOptions
{
    public const string SectionName = "WorkingMemory";

    public int MaxTokenWindow { get; set; } = 8192;

    public int SummarizationThreshold { get; set; } = 6144;

    public string SummarizationModel { get; set; } = "gpt-4o-mini";
}
