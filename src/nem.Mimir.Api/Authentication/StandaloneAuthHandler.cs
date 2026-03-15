using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace nem.Mimir.Api.Authentication;

/// <summary>
/// A simple authentication handler that auto-authenticates every request
/// as a dev admin user. Only used when StandaloneMode is enabled.
/// </summary>
public class StandaloneAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "Standalone";

    public StandaloneAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "standalone-dev-user"),
            new Claim(ClaimTypes.Name, "Dev Admin"),
            new Claim(ClaimTypes.Email, "admin@mimir.local"),
            new Claim(ClaimTypes.Role, "admin"),
            new Claim(ClaimTypes.Role, "user"),
            new Claim("preferred_username", "admin"),
        };

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
