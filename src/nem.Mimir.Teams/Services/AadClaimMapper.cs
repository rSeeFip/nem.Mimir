using System.Security.Claims;

namespace nem.Mimir.Teams.Services;

internal sealed class AadClaimMapper
{
    private const string ObjectIdClaimType = "http://schemas.microsoft.com/identity/claims/objectidentifier";

    public (string UserId, string? DisplayName) Map(ClaimsIdentity claims)
    {
        var userId = claims.FindFirst(ObjectIdClaimType)?.Value
                     ?? claims.FindFirst(ClaimTypes.NameIdentifier)?.Value
                     ?? "unknown";

        var displayName = claims.FindFirst("name")?.Value;

        return (userId, displayName);
    }
}
