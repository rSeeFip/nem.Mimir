using System.Net;
using Shouldly;

namespace Mimir.E2E.Tests;

[Collection("E2E")]
public sealed class E2EFactoryHealthTests
{
    private readonly E2EWebApplicationFactory _factory;

    public E2EFactoryHealthTests(E2EWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact(DisplayName = "E2EFactoryHealth: factory can serve /health")]
    public async Task E2EFactoryHealth_ReturnsOk()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
