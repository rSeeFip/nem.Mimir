using System.Net;
using Shouldly;

namespace nem.Mimir.E2E.Tests;

/// <summary>
/// E2E tests verifying JWT authentication enforcement.
/// </summary>
[Collection(E2ETestCollection.Name)]
public sealed class AuthTests
{
    private readonly E2EWebApplicationFactory _factory;

    public AuthTests(E2EWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("/v1/chat/completions", "POST")]
    [InlineData("/v1/models", "GET")]
    [InlineData("/api/conversations", "GET")]
    public async Task ProtectedEndpoint_WithoutToken_Returns401(string endpoint, string method)
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var request = new HttpRequestMessage(new HttpMethod(method), endpoint);
        if (method == "POST")
            request.Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithValidToken_DoesNotReturn401()
    {
        // Arrange
        var client = _factory.CreateAuthenticatedClient();

        // Act
        var response = await client.GetAsync("/v1/models");

        // Assert
        response.StatusCode.ShouldNotBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithExpiredToken_Returns401()
    {
        // Arrange — generate a token that's already expired by using a very old date
        var client = _factory.CreateClient();
        var token = GenerateExpiredToken();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.GetAsync("/v1/models");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private static string GenerateExpiredToken()
    {
        var key = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
            System.Text.Encoding.UTF8.GetBytes(Helpers.JwtTokenHelper.TestSigningKey));
        var credentials = new Microsoft.IdentityModel.Tokens.SigningCredentials(
            key, Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256);

        var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(
            issuer: Helpers.JwtTokenHelper.TestIssuer,
            audience: Helpers.JwtTokenHelper.TestAudience,
            claims: new[]
            {
                new System.Security.Claims.Claim(
                    System.Security.Claims.ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            },
            expires: DateTime.UtcNow.AddHours(-1), // Already expired
            signingCredentials: credentials);

        return new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(token);
    }
}
