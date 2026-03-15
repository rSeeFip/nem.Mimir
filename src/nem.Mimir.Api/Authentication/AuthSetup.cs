using Microsoft.AspNetCore.Authentication;
using nem.Contracts.AspNetCore.Auth;

namespace nem.Mimir.Api.Authentication;

/// <summary>
/// Configures authentication for the Mimir API.
/// In StandaloneMode, uses a simple handler that auto-authenticates as a dev admin.
/// Otherwise, delegates to the shared Keycloak auth from nem.Contracts.
/// </summary>
public static class AuthSetup
{
    public static IServiceCollection AddMimirAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        bool standaloneMode)
    {
        if (standaloneMode)
        {
            services.AddAuthentication(StandaloneAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, StandaloneAuthHandler>(
                    StandaloneAuthHandler.SchemeName, _ => { });
        }
        else
        {
            services.AddNemKeycloakAuth(configuration);
        }

        return services;
    }
}
