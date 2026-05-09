using System.Collections.Concurrent;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using nem.Mimir.Telegram.Configuration;

namespace nem.Mimir.Telegram.Services;

/// <summary>
/// Manages authentication with Keycloak and one-time auth code generation.
/// </summary>
internal sealed class AuthenticationService
{
    private readonly ConcurrentDictionary<string, AuthCodeEntry> _authCodes = new();
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly TelegramSettings _settings;
    private readonly ILogger<AuthenticationService> _logger;
    private readonly Timer _cleanupTimer;

    public AuthenticationService(
        IHttpClientFactory httpClientFactory,
        IOptions<TelegramSettings> settings,
        ILogger<AuthenticationService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings.Value;
        _logger = logger;

        // Periodic cleanup of expired auth codes every 60 seconds
        _cleanupTimer = new Timer(CleanupExpiredCodes, null, TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(60));
    }

    /// <summary>
    /// Generates a one-time auth code for the given Telegram user.
    /// </summary>
    public string GenerateAuthCode(long telegramUserId)
    {
        var code = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var entry = new AuthCodeEntry(telegramUserId, DateTimeOffset.UtcNow.AddMinutes(_settings.AuthCodeExpiryMinutes));

        _authCodes[code] = entry;
        _logger.LogDebug("Generated auth code for Telegram user {UserId}", telegramUserId);

        return code;
    }

    /// <summary>
    /// Validates an auth code and returns the associated Telegram user ID if valid.
    /// </summary>
    public long? ValidateAuthCode(string code)
    {
        if (!_authCodes.TryRemove(code.ToUpperInvariant(), out var entry))
        {
            return null;
        }

        if (entry.ExpiresAt < DateTimeOffset.UtcNow)
        {
            _logger.LogDebug("Auth code expired for Telegram user {UserId}", entry.TelegramUserId);
            return null;
        }

        return entry.TelegramUserId;
    }

    /// <summary>
    /// Exchanges user credentials for a Keycloak access token.
    /// </summary>
    public async Task<string?> AuthenticateWithKeycloakAsync(string username, string password, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_settings.KeycloakUrl))
        {
            _logger.LogWarning("Keycloak URL is not configured");
            return null;
        }

        try
        {
            using var client = _httpClientFactory.CreateClient("keycloak");
            var tokenEndpoint = $"{_settings.KeycloakUrl.TrimEnd('/')}/protocol/openid-connect/token";

            var requestContent = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "password",
                ["client_id"] = _settings.KeycloakClientId,
                ["client_secret"] = _settings.KeycloakClientSecret,
                ["username"] = username,
                ["password"] = password,
                ["scope"] = "openid"
            });

            var response = await client.PostAsync(tokenEndpoint, requestContent, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Keycloak authentication failed with status {StatusCode}", response.StatusCode);
                return null;
            }

            var tokenResponse = await response.Content.ReadFromJsonAsync<KeycloakTokenResponse>(ct);
            return tokenResponse?.AccessToken;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error authenticating with Keycloak");
            return null;
        }
    }

    private void CleanupExpiredCodes(object? state)
    {
        var now = DateTimeOffset.UtcNow;
        var expiredKeys = _authCodes
            .Where(kvp => kvp.Value.ExpiresAt < now)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in expiredKeys)
        {
            _authCodes.TryRemove(key, out _);
        }

        if (expiredKeys.Count > 0)
        {
            _logger.LogDebug("Cleaned up {Count} expired auth codes", expiredKeys.Count);
        }
    }
}

internal sealed record AuthCodeEntry(long TelegramUserId, DateTimeOffset ExpiresAt);

internal sealed record KeycloakTokenResponse
{
    [System.Text.Json.Serialization.JsonPropertyName("access_token")]
    public string AccessToken { get; init; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("expires_in")]
    public int ExpiresIn { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("token_type")]
    public string TokenType { get; init; } = string.Empty;
}
