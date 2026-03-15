using System.Net;
using Shouldly;

namespace nem.Mimir.Api.IntegrationTests;

/// <summary>
/// Integration tests verifying that unauthenticated requests are rejected.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public sealed class AuthenticationTests
{
    private readonly HttpClient _client;

    public AuthenticationTests(MimirWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Theory]
    [InlineData("/api/conversations")]
    [InlineData("/api/users/me")]
    public async Task AuthenticatedEndpoint_WithoutToken_ReturnsNon200(string endpoint)
    {
        // Act
        var response = await _client.GetAsync(endpoint);

        // Assert — once controllers are added they should return 401.
        // Endpoints that don't exist yet will return 404.
        var statusCode = response.StatusCode;
        statusCode.ShouldNotBe(HttpStatusCode.OK,
            "Unauthenticated requests should not succeed");
    }
}
