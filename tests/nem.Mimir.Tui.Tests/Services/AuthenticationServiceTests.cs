using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;
using nem.Mimir.Tui.Models;
using nem.Mimir.Tui.Services;
using NSubstitute;
using Shouldly;

namespace nem.Mimir.Tui.Tests.Services;

public sealed class AuthenticationServiceTests
{
    private static readonly TuiSettings DefaultSettings = new()
    {
        KeycloakUrl = "http://localhost:8080/realms/mimir/protocol/openid-connect/token",
        KeycloakClientId = "mimir-api",
    };

    [Fact]
    public async Task LoginAsync_SuccessfulResponse_SetsAccessTokenAndReturnsTrue()
    {
        // Arrange
        var tokenJson = JsonSerializer.Serialize(new
        {
            access_token = "test-jwt-token",
            expires_in = 300,
            token_type = "Bearer",
        });
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, tokenJson);
        using var httpClient = new HttpClient(handler);
        var service = new AuthenticationService(httpClient, Options.Create(DefaultSettings));

        // Act
        var result = await service.LoginAsync("admin", "admin");

        // Assert
        result.ShouldBeTrue();
        service.IsAuthenticated.ShouldBeTrue();
        service.AccessToken.ShouldBe("test-jwt-token");
    }

    [Fact]
    public async Task LoginAsync_UnauthorizedResponse_ReturnsFalseAndNotAuthenticated()
    {
        // Arrange
        var handler = new FakeHttpMessageHandler(HttpStatusCode.Unauthorized, "{}");
        using var httpClient = new HttpClient(handler);
        var service = new AuthenticationService(httpClient, Options.Create(DefaultSettings));

        // Act
        var result = await service.LoginAsync("wrong", "credentials");

        // Assert
        result.ShouldBeFalse();
        service.IsAuthenticated.ShouldBeFalse();
        service.AccessToken.ShouldBeNull();
    }

    [Fact]
    public async Task LoginAsync_NullTokenInResponse_ReturnsFalse()
    {
        // Arrange
        var tokenJson = JsonSerializer.Serialize(new
        {
            access_token = (string?)null,
            expires_in = 300,
            token_type = "Bearer",
        });
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, tokenJson);
        using var httpClient = new HttpClient(handler);
        var service = new AuthenticationService(httpClient, Options.Create(DefaultSettings));

        // Act
        var result = await service.LoginAsync("admin", "admin");

        // Assert
        result.ShouldBeFalse();
        service.IsAuthenticated.ShouldBeFalse();
    }

    [Fact]
    public void IsAuthenticated_BeforeLogin_ReturnsFalse()
    {
        // Arrange
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, "{}");
        using var httpClient = new HttpClient(handler);
        var service = new AuthenticationService(httpClient, Options.Create(DefaultSettings));

        // Act & Assert
        service.IsAuthenticated.ShouldBeFalse();
        service.AccessToken.ShouldBeNull();
    }

    [Fact]
    public async Task Logout_ClearsAuthenticationState()
    {
        // Arrange
        var tokenJson = JsonSerializer.Serialize(new
        {
            access_token = "test-jwt-token",
            expires_in = 300,
            token_type = "Bearer",
        });
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, tokenJson);
        using var httpClient = new HttpClient(handler);
        var service = new AuthenticationService(httpClient, Options.Create(DefaultSettings));
        await service.LoginAsync("admin", "admin");

        // Act
        service.Logout();

        // Assert
        service.IsAuthenticated.ShouldBeFalse();
        service.AccessToken.ShouldBeNull();
    }

    [Fact]
    public async Task LoginAsync_SendsCorrectFormData()
    {
        // Arrange
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, JsonSerializer.Serialize(new
        {
            access_token = "token",
            expires_in = 300,
            token_type = "Bearer",
        }));
        using var httpClient = new HttpClient(handler);
        var service = new AuthenticationService(httpClient, Options.Create(DefaultSettings));

        // Act
        await service.LoginAsync("testuser", "testpass");

        // Assert
        handler.LastRequestContent.ShouldNotBeNull();
        var formData = await handler.LastRequestContent.ReadAsStringAsync();
        formData.ShouldContain("grant_type=password");
        formData.ShouldContain("client_id=mimir-api");
        formData.ShouldContain("username=testuser");
        formData.ShouldContain("password=testpass");
    }

    /// <summary>
    /// A simple fake HTTP handler for testing without external dependencies.
    /// </summary>
    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _responseContent;

        public HttpContent? LastRequestContent { get; private set; }

        public FakeHttpMessageHandler(HttpStatusCode statusCode, string responseContent)
        {
            _statusCode = statusCode;
            _responseContent = responseContent;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestContent = request.Content;
            var response = new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_responseContent, System.Text.Encoding.UTF8, "application/json"),
            };
            return Task.FromResult(response);
        }
    }
}
