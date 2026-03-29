namespace nem.Mimir.Application.Agents.Selection;

public interface ISelectionStep
{
    Task<SelectionContext> ExecuteAsync(SelectionContext context, CancellationToken ct);
}
