namespace nem.Mimir.Infrastructure.Billing;

using System.Security.Cryptography;
using System.Text;

public class PersistedCostEvent
{
    public Guid Id { get; set; }

    public string TenantId { get; set; } = string.Empty;

    public string UserId { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;

    public int PromptTokens { get; set; }

    public int CompletionTokens { get; set; }

    public int TotalTokens { get; set; }

    public decimal CostUsd { get; set; }

    public string Channel { get; set; } = string.Empty;

    public string IdempotencyKey { get; set; } = string.Empty;

    public DateTimeOffset OccurredAt { get; set; }

    /// <summary>
    /// Computes a deterministic SHA-256 hash of (TenantId + UserId + Model + OccurredAt)
    /// to prevent duplicate persistence on Wolverine outbox replay.
    /// </summary>
    public static string ComputeIdempotencyKey(string tenantId, string userId, string model, DateTimeOffset occurredAt)
    {
        var raw = $"{tenantId}|{userId}|{model}|{occurredAt:O}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
