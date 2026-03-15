using System.Net;
using Microsoft.Extensions.Options;
using nem.Mimir.Tui.Models;
using nem.Mimir.Tui.Services;
using Shouldly;

namespace nem.Mimir.Tui.Tests.Services;

public sealed class ModelServiceTests
{
    private static TuiSettings CreateSettings(string defaultModel = "phi-4-mini") => new()
    {
        DefaultModel = defaultModel,
    };

    private static AuthenticationService CreateAuthService()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, "{}");
        var httpClient = new HttpClient(handler);
        return new AuthenticationService(httpClient, Options.Create(new TuiSettings()));
    }

    [Fact]
    public void CurrentModel_DefaultsToSettingsValue()
    {
        // Arrange
        var settings = CreateSettings("qwen-2.5-72b");
        var authService = CreateAuthService();
        using var httpClient = new HttpClient(new FakeHttpMessageHandler(HttpStatusCode.OK, "[]"));
        var service = new ModelService(httpClient, authService, Options.Create(settings));

        // Act & Assert
        service.CurrentModel.ShouldBe("qwen-2.5-72b");
    }

    [Theory]
    [InlineData("phi-4-mini", true)]
    [InlineData("qwen-2.5-72b", true)]
    [InlineData("qwen-2.5-coder-32b", true)]
    [InlineData("unknown-model", false)]
    [InlineData("", false)]
    [InlineData("PHI-4-MINI", false)] // Case-sensitive check
    public void IsKnownModel_ReturnsExpectedResult(string modelName, bool expected)
    {
        ModelService.IsKnownModel(modelName).ShouldBe(expected);
    }

    [Fact]
    public void TrySetModel_KnownModel_ReturnsTrueAndSetsCurrentModel()
    {
        // Arrange
        var authService = CreateAuthService();
        using var httpClient = new HttpClient(new FakeHttpMessageHandler(HttpStatusCode.OK, "[]"));
        var service = new ModelService(httpClient, authService, Options.Create(CreateSettings()));

        // Act
        var result = service.TrySetModel("qwen-2.5-72b");

        // Assert
        result.ShouldBeTrue();
        service.CurrentModel.ShouldBe("qwen-2.5-72b");
    }

    [Fact]
    public void TrySetModel_UnknownModel_ReturnsFalseAndKeepsCurrentModel()
    {
        // Arrange
        var authService = CreateAuthService();
        using var httpClient = new HttpClient(new FakeHttpMessageHandler(HttpStatusCode.OK, "[]"));
        var service = new ModelService(httpClient, authService, Options.Create(CreateSettings()));

        // Act
        var result = service.TrySetModel("nonexistent");

        // Assert
        result.ShouldBeFalse();
        service.CurrentModel.ShouldBe("phi-4-mini");
    }

    [Fact]
    public void TrySetModel_NormalizesInputToLowerCase()
    {
        // Arrange
        var authService = CreateAuthService();
        using var httpClient = new HttpClient(new FakeHttpMessageHandler(HttpStatusCode.OK, "[]"));
        var service = new ModelService(httpClient, authService, Options.Create(CreateSettings()));

        // Act
        var result = service.TrySetModel("PHI-4-MINI");

        // Assert
        result.ShouldBeTrue();
        service.CurrentModel.ShouldBe("phi-4-mini");
    }

    [Fact]
    public void TrySetModel_TrimsWhitespace()
    {
        // Arrange
        var authService = CreateAuthService();
        using var httpClient = new HttpClient(new FakeHttpMessageHandler(HttpStatusCode.OK, "[]"));
        var service = new ModelService(httpClient, authService, Options.Create(CreateSettings()));

        // Act
        var result = service.TrySetModel("  phi-4-mini  ");

        // Assert
        result.ShouldBeTrue();
        service.CurrentModel.ShouldBe("phi-4-mini");
    }

    [Fact]
    public void CurrentModel_CanBeSetDirectly()
    {
        // Arrange
        var authService = CreateAuthService();
        using var httpClient = new HttpClient(new FakeHttpMessageHandler(HttpStatusCode.OK, "[]"));
        var service = new ModelService(httpClient, authService, Options.Create(CreateSettings()));

        // Act
        service.CurrentModel = "custom-model";

        // Assert
        service.CurrentModel.ShouldBe("custom-model");
    }

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _responseContent;

        public FakeHttpMessageHandler(HttpStatusCode statusCode, string responseContent)
        {
            _statusCode = statusCode;
            _responseContent = responseContent;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_responseContent, System.Text.Encoding.UTF8, "application/json"),
            };
            return Task.FromResult(response);
        }
    }
}
