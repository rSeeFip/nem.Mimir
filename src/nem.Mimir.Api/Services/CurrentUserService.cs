using System.Security.Claims;
using System.Text.Json;
using nem.Mimir.Application.Common.Interfaces;

namespace nem.Mimir.Api.Services;

/// <summary>
/// Extracts current user information from the HTTP context JWT claims.
/// </summary>
public sealed class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc />
    public string? UserId =>
        _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);

    /// <inheritdoc />
    public bool IsAuthenticated =>
        _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;

    /// <inheritdoc />
    public IReadOnlyList<string> Roles
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user is null)
                return [];

            // Try standard role claims first
            var roles = user.FindAll(ClaimTypes.Role)
                .Select(c => c.Value)
                .ToList();

            if (roles.Count > 0)
                return roles.AsReadOnly();

            // Fall back to Keycloak realm_access.roles claim
            var realmAccessClaim = user.FindFirstValue("realm_access");
            if (realmAccessClaim is null)
                return [];

            try
            {
                using var doc = JsonDocument.Parse(realmAccessClaim);
                if (doc.RootElement.TryGetProperty("roles", out var rolesElement) &&
                    rolesElement.ValueKind == JsonValueKind.Array)
                {
                    return rolesElement.EnumerateArray()
                        .Where(e => e.ValueKind == JsonValueKind.String)
                        .Select(e => e.GetString()!)
                        .ToList()
                        .AsReadOnly();
                }
            }
            catch (JsonException)
            {
                // Malformed realm_access claim — return empty
            }

            return [];
        }
    }
}
