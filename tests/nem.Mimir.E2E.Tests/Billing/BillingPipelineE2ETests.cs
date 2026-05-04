using System.Net;
using System.Net.Http.Json;
using Shouldly;
using nem.Mimir.Application.Billing;

namespace nem.Mimir.E2E.Tests.Billing;

[Collection(E2ETestCollection.Name)]
public sealed class BillingPipelineE2ETests
{
    private readonly E2EWebApplicationFactory _factory;

    public BillingPipelineE2ETests(E2EWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static string BuildUsageUrl(string path, DateTimeOffset from, DateTimeOffset to)
    {
        var fromEncoded = Uri.EscapeDataString(from.ToString("O"));
        var toEncoded = Uri.EscapeDataString(to.ToString("O"));
        return $"{path}?from={fromEncoded}&to={toEncoded}";
    }

    [Fact]
    public async Task GetUsage_WithoutAuthentication_Returns401()
    {
        var client = _factory.CreateClient();
        var url = BuildUsageUrl("/api/billing/usage", DateTimeOffset.UtcNow.AddDays(-7), DateTimeOffset.UtcNow);

        var response = await client.GetAsync(url);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetUsageByModel_WithoutAuthentication_Returns401()
    {
        var client = _factory.CreateClient();
        var url = BuildUsageUrl("/api/billing/usage/models", DateTimeOffset.UtcNow.AddDays(-7), DateTimeOffset.UtcNow);

        var response = await client.GetAsync(url);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetUsage_WithAuthentication_Returns200WithSummary()
    {
        var client = _factory.CreateAuthenticatedClient();
        var url = BuildUsageUrl("/api/billing/usage", DateTimeOffset.UtcNow.AddDays(-7), DateTimeOffset.UtcNow);

        var response = await client.GetAsync(url);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var summary = await response.Content.ReadFromJsonAsync<TenantUsageSummary>();
        summary.ShouldNotBeNull();
        summary.TotalCost.ShouldBeGreaterThanOrEqualTo(0);
        summary.TotalInputTokens.ShouldBeGreaterThanOrEqualTo(0);
        summary.TotalOutputTokens.ShouldBeGreaterThanOrEqualTo(0);
        summary.RequestCount.ShouldBeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task GetUsageByModel_WithAuthentication_Returns200WithBreakdown()
    {
        var client = _factory.CreateAuthenticatedClient();
        var url = BuildUsageUrl("/api/billing/usage/models", DateTimeOffset.UtcNow.AddDays(-7), DateTimeOffset.UtcNow);

        var response = await client.GetAsync(url);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var breakdown = await response.Content.ReadFromJsonAsync<Dictionary<string, ModelUsage>>();
        breakdown.ShouldNotBeNull();
    }

    [Fact]
    public async Task GetUsage_WithFutureDateRange_ReturnsEmptySummary()
    {
        var client = _factory.CreateAuthenticatedClient();
        var url = BuildUsageUrl("/api/billing/usage", DateTimeOffset.UtcNow.AddYears(1), DateTimeOffset.UtcNow.AddYears(2));

        var response = await client.GetAsync(url);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var summary = await response.Content.ReadFromJsonAsync<TenantUsageSummary>();
        summary.ShouldNotBeNull();
        summary.TotalCost.ShouldBe(0);
        summary.TotalInputTokens.ShouldBe(0);
        summary.TotalOutputTokens.ShouldBe(0);
        summary.RequestCount.ShouldBe(0);
    }
}
