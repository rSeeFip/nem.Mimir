using nem.Contracts.Federation;
using nem.Contracts.Interfaces;

namespace nem.Mimir.Infrastructure.Federation;

public sealed class MutableFederationContextAccessor : IFederationContextAccessor, ITenantContext
{
    private FederationTenantContext? _context;

    public FederationTenantContext Current => _context ?? FederationTenantContext.Empty;

    public string TenantId => Current.InstanceId.IsEmpty ? string.Empty : Current.InstanceId.ToString();
    public string? TenantName => null;

    public void SetContext(FederationTenantContext context)
    {
        _context = context;
    }
}
