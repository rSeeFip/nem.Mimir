using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace nem.Mimir.E2E.Tests.Helpers;

/// <summary>
/// Helper for generating JWT tokens for E2E testing.
/// Uses a symmetric key that matches the test server's configuration.
/// </summary>
public static class JwtTokenHelper
{
    /// <summary>
    /// The symmetric key used for both signing and validating test tokens.
    /// Must be at least 32 bytes (256 bits) for HS256.
    /// </summary>
    public const string TestSigningKey = "E2E-Test-Signing-Key-That-Is-Long-Enough-For-HS256-Algorithm!!";
    public const string TestIssuer = "https://test-authority.local";
    public const string TestAudience = "mimir-api";

    /// <summary>
    /// Generates a valid JWT token for testing purposes.
    /// </summary>
    /// <param name="userId">The user ID to embed in the token.</param>
    /// <param name="email">The email to embed in the token.</param>
    /// <param name="roles">Optional roles to add to the token.</param>
    /// <returns>A signed JWT token string.</returns>
    public static string GenerateToken(
        string? userId = null,
        string? email = null,
        IEnumerable<string>? roles = null,
        string? tenantId = null,
        string? tenantName = null)
    {
        userId ??= Guid.NewGuid().ToString();
        email ??= $"testuser-{userId[..8]}@test.local";
        tenantId ??= "default";
        tenantName ??= tenantId;

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestSigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId),
            new(ClaimTypes.NameIdentifier, userId),
            new(JwtRegisteredClaimNames.Email, email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("tenant_id", tenantId),
            new("tenant_name", tenantName),
        };

        var effectiveRoles = (roles ?? ["user"])
            .SelectMany(role => role.Equals("admin", StringComparison.OrdinalIgnoreCase)
                ? new[] { role, "platform-admin" }
                : new[] { role })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var role in effectiveRoles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var token = new JwtSecurityToken(
            issuer: TestIssuer,
            audience: TestAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
