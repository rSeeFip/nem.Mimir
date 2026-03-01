using System.Net;
using System.Text.Json;
using Shouldly;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace Mimir.E2E.Tests;

/// <summary>
/// E2E tests for GET /v1/models endpoint.
/// Verifies that the models endpoint returns data from the WireMock LiteLLM proxy.
/// </summary>
[Collection(E2ETestCollection.Name)]
public sealed class ModelsTests
{
    private readonly E2EWebApplicationFactory _factory;

    public ModelsTests(E2EWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ListModels_WithValidToken_ReturnsModels()
    {
        // Arrange — re-register the WireMock stub to ensure it's fresh
        _factory.WireMock
            .Given(Request.Create()
                .WithPath("/v1/models")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(@"{
                    ""object"": ""list"",
                    ""data"": [
                        {
                            ""id"": ""qwen-2.5-72b"",
                            ""object"": ""model"",
                            ""created"": 1234567890,
                            ""owned_by"": ""litellm""
                        }
                    ]
                }"));

        var client = _factory.CreateAuthenticatedClient();

        // Act
        var response = await client.GetAsync("/v1/models");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        body.ShouldNotBeNullOrWhiteSpace();

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        root.TryGetProperty("data", out var data).ShouldBeTrue();
        data.GetArrayLength().ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task ListModels_WithoutToken_Returns401()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/v1/models");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
