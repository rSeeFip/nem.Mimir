using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using nem.Mimir.Tui.Models;

namespace nem.Mimir.Tui.Services;

/// <summary>
/// Handles authentication against Keycloak using the resource owner password credentials (direct access) grant.
/// </summary>
internal sealed class AuthenticationService
{
    private readonly HttpClient _httpClient;
    private readonly TuiSettings _settings;
    private string? _accessToken;
    private DateTimeOffset _tokenExpiry = DateTimeOffset.MinValue;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public AuthenticationService(HttpClient httpClient, IOptions<TuiSettings> options)
    {
        _httpClient = httpClient;
        _settings = options.Value;
    }

    /// <summary>
    /// Gets the current access token, or null if not authenticated.
    /// </summary>
    public string? AccessToken => _accessToken;

    /// <summary>
    /// Gets a value indicating whether the current token is valid and not expired.
    /// </summary>
    public bool IsAuthenticated => _accessToken is not null && DateTimeOffset.UtcNow < _tokenExpiry;

    /// <summary>
    /// Authenticates with Keycloak using username and password.
    /// </summary>
    /// <param name="username">The username.</param>
    /// <param name="password">The password.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if authentication succeeded; false otherwise.</returns>
    public async Task<bool> LoginAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["client_id"] = _settings.KeycloakClientId,
            ["username"] = username,
            ["password"] = password,
        });

        var response = await _httpClient.PostAsync(_settings.KeycloakUrl, content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _accessToken = null;
            return false;
        }

        var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>(JsonOptions, cancellationToken);

        if (tokenResponse is null || string.IsNullOrWhiteSpace(tokenResponse.AccessToken))
        {
            _accessToken = null;
            return false;
        }

        _accessToken = tokenResponse.AccessToken;
        _tokenExpiry = DateTimeOffset.UtcNow.AddSeconds(tokenResponse.ExpiresIn - 30);
        return true;
    }

    /// <summary>
    /// Clears the current authentication state.
    /// </summary>
    public void Logout()
    {
        _accessToken = null;
        _tokenExpiry = DateTimeOffset.MinValue;
    }
}
