using nem.Mimir.Domain.Tenants;

namespace nem.Mimir.Application.Tenants.Dtos;

public sealed record TenantDto(
    Guid Id,
    string Name,
    string Slug,
    TenantStatus Status,
    int DefaultRateLimit,
    DateTimeOffset CreatedAt,
    DateTimeOffset? OffboardedAt,
    DateTimeOffset? DataRetentionUntil);
