namespace Mimir.Tui.Models;

/// <summary>
/// Keycloak token response from the direct access grant (password) flow.
/// </summary>
internal sealed record TokenResponse(
    string AccessToken,
    int ExpiresIn,
    string TokenType);
