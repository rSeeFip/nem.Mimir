namespace nem.Mimir.Application.Agents.Selection;

public sealed record SelectionStepDefinition(
    string Name,
    double Weight = 1d,
    double? SuccessRateThreshold = null,
    double? Bm25K1 = null,
    double? Bm25B = null)
{
    public static SelectionStepDefinition Quality(double weight = 1d, double successRateThreshold = 0.5d)
        => new(SelectionStepNames.Quality, weight, successRateThreshold);

    public static SelectionStepDefinition Bm25(double weight = 1d, double k1 = 1.2d, double b = 0.75d)
        => new(SelectionStepNames.Bm25, weight, null, k1, b);

    public static SelectionStepDefinition Embedding(double weight = 1d)
        => new(SelectionStepNames.Embedding, weight);
}
