using MediatR;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Tenants.Dtos;
using nem.Mimir.Domain.Tenants;

namespace nem.Mimir.Application.Tenants.Queries;

/// <summary>
/// Lists all tenants for administration purposes.
/// </summary>
public sealed record GetTenantsQuery : IQuery<IReadOnlyList<TenantDto>>;

internal sealed class GetTenantsQueryHandler(ITenantStore tenantStore)
    : IRequestHandler<GetTenantsQuery, IReadOnlyList<TenantDto>>
{
    public async Task<IReadOnlyList<TenantDto>> Handle(GetTenantsQuery request, CancellationToken cancellationToken)
    {
        var tenants = await tenantStore.GetAllAsync(cancellationToken);
        return tenants.Select(TenantMappings.ToDto).ToList();
    }
}
