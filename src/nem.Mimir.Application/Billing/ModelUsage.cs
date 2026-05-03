namespace nem.Mimir.Application.Billing;

public class ModelUsage
{
    public string Model { get; set; } = string.Empty;

    public long InputTokens { get; set; }

    public long OutputTokens { get; set; }

    public decimal Cost { get; set; }

    public int RequestCount { get; set; }
}
