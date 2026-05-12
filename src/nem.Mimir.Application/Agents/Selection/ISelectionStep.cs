namespace nem.Mimir.Application.Agents.Selection;

public interface ISelectionStep
{
    string Name { get; }

    Task<SelectionContext> ExecuteAsync(SelectionContext context, CancellationToken ct);
}
