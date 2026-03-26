namespace nem.Mimir.Domain.ValueObjects;

public sealed record ModelCapability(
    bool SupportsVision,
    bool SupportsFunctionCalling,
    bool SupportsStreaming,
    bool SupportsJsonMode)
{
    public static readonly ModelCapability Default = new(
        SupportsVision: false,
        SupportsFunctionCalling: false,
        SupportsStreaming: true,
        SupportsJsonMode: false);
}
