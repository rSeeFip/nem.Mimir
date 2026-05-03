using System.Net;
using Shouldly;

namespace nem.Mimir.E2E.Tests;

[Collection(E2ETestCollection.Name)]
public sealed class E2EFactoryHealthTests
{
    private readonly E2EWebApplicationFactory _factory;

    public E2EFactoryHealthTests(E2EWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact(DisplayName = "E2EFactoryHealthCheck: factory can serve /health")]
    public async Task E2EFactoryHealthCheck_ReturnsOk()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);
    }
}
