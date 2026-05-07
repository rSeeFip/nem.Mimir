using System.Globalization;
using System.Net;
using Shouldly;
using Xunit.Abstractions;

namespace nem.Mimir.Api.IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public sealed class RateLimitingTests
{
    private const string RateLimitedEndpoint = "/api/models";

    private readonly MimirWebApplicationFactory _factory;
    private readonly ITestOutputHelper _output;

    public RateLimitingTests(MimirWebApplicationFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
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
        _output.WriteLine($"Tenant A received {rateLimitedResponse.StatusCode}; tenant B received {otherTenantResponse.StatusCode}.");
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

        _output.WriteLine($"429 Retry-After: {retryAfterValue}");
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
