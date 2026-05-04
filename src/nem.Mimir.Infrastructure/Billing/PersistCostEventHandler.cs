namespace nem.Mimir.Infrastructure.Billing;

using Marten;
using nem.Contracts.Costs;

public sealed class PersistCostEventHandler
{
    public async Task Handle(CostEvent costEvent, IDocumentSession session, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(costEvent);
        ArgumentNullException.ThrowIfNull(session);

        var tenantId = costEvent.TenantId ?? string.Empty;
        var userId = GetTagValue(costEvent.Tags, "userId", "user_id") ?? string.Empty;
        var model = GetTagValue(costEvent.Tags, "model", "model_name") ?? string.Empty;
        var promptTokens = GetIntTagValue(costEvent.Tags, "promptTokens", "prompt_tokens", "inputTokens", "input_tokens");
        var completionTokens = GetIntTagValue(costEvent.Tags, "completionTokens", "completion_tokens", "outputTokens", "output_tokens");
        var totalTokens = GetTotalTokens(costEvent, promptTokens, completionTokens);
        var idempotencyKey = PersistedCostEvent.ComputeIdempotencyKey(tenantId, userId, model, costEvent.Timestamp);

        var exists = await session.Query<PersistedCostEvent>()
            .AnyAsync(x => x.IdempotencyKey == idempotencyKey, ct);

        if (exists)
        {
            return;
        }

        var persisted = new PersistedCostEvent
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = userId,
            Model = model,
            PromptTokens = promptTokens,
            CompletionTokens = completionTokens,
            TotalTokens = totalTokens,
            CostUsd = costEvent.AmortizedCost,
            Channel = GetTagValue(costEvent.Tags, "channel") ?? string.Empty,
            IdempotencyKey = idempotencyKey,
            OccurredAt = costEvent.Timestamp,
        };

        session.Store(persisted);
        await session.SaveChangesAsync(ct);
    }

    private static string? GetTagValue(IReadOnlyDictionary<string, string> tags, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (tags.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static int GetIntTagValue(IReadOnlyDictionary<string, string> tags, params string[] keys)
    {
        var raw = GetTagValue(tags, keys);
        return int.TryParse(raw, out var parsed) ? parsed : 0;
    }

    private static int GetTotalTokens(CostEvent costEvent, int promptTokens, int completionTokens)
    {
        var taggedTotal = GetIntTagValue(costEvent.Tags, "totalTokens", "total_tokens");
        if (taggedTotal > 0)
        {
            return taggedTotal;
        }

        var calculatedTotal = promptTokens + completionTokens;
        if (calculatedTotal > 0)
        {
            return calculatedTotal;
        }

        if (!string.Equals(costEvent.UsageUnit, "tokens", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (costEvent.UsageQuantity < 0 || costEvent.UsageQuantity > int.MaxValue)
        {
            return 0;
        }

        return decimal.Truncate(costEvent.UsageQuantity) == costEvent.UsageQuantity
            ? (int)costEvent.UsageQuantity
            : 0;
    }
}
