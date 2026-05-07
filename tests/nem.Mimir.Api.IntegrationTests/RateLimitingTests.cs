using System.Globalization;
using System.Net;
using Shouldly;

namespace nem.Mimir.Api.IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public sealed class RateLimitingTests
{
    private const string RateLimitedEndpoint = "/api/models";

    private readonly MimirWebApplicationFactory _factory;

    public RateLimitingTests(MimirWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task TenantALimit_DoesNotAffectTenantB()
    {
        var sharedUserId = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
        var tenantA = $"tenant-a-{Guid.NewGuid():N}";
        var tenantB = $"tenant-b-{Guid.NewGuid():N}";

        using var tenantAClient = _factory.CreateAuthenticatedClient(userId: sharedUserId, tenantId: tenantA);
        using var tenantBClient = _factory.CreateAuthenticatedClient(userId: sharedUserId, tenantId: tenantB);

        var rateLimitedResponse = await ExhaustRateLimitAsync(tenantAClient);

        rateLimitedResponse.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);

        var otherTenantResponse = await tenantBClient.GetAsync(RateLimitedEndpoint);

        otherTenantResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        Console.WriteLine($"TENANT_ISOLATION tenantA={tenantA} limitedStatus={(int)rateLimitedResponse.StatusCode} tenantB={tenantB} unaffectedStatus={(int)otherTenantResponse.StatusCode}");
    }

    [Fact]
    public async Task RateLimitedResponse_IncludesRetryAfterHeader()
    {
        var tenantId = $"tenant-retry-{Guid.NewGuid():N}";
        using var client = _factory.CreateAuthenticatedClient(userId: Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture), tenantId: tenantId);

        var response = await ExhaustRateLimitAsync(client);

        response.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        response.Headers.TryGetValues("Retry-After", out var retryAfterValues).ShouldBeTrue();

        var retryAfterValue = retryAfterValues!.Single();
        int.TryParse(retryAfterValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var retryAfterSeconds)
            .ShouldBeTrue();
        retryAfterSeconds.ShouldBeGreaterThan(0);
        Console.WriteLine($"RATE_LIMIT_429 status={(int)response.StatusCode} retryAfter={retryAfterValue}");
    }

    private static async Task<HttpResponseMessage> ExhaustRateLimitAsync(HttpClient client)
    {
        HttpResponseMessage? lastResponse = null;

        for (var attempt = 1; attempt <= 150; attempt++)
        {
            lastResponse = await client.GetAsync(RateLimitedEndpoint);

            if (lastResponse.StatusCode == HttpStatusCode.TooManyRequests)
            {
                return lastResponse;
            }

            lastResponse.StatusCode.ShouldBe(HttpStatusCode.OK, $"Expected 200 before hitting limit on attempt {attempt}.");
        }

        throw new ShouldAssertException($"Expected a 429 response within 150 requests, last status was {lastResponse?.StatusCode}.");
    }
}
