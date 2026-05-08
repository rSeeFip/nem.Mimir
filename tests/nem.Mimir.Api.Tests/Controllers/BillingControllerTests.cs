using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using nem.Mimir.Api.Controllers;
using nem.Mimir.Application.Billing;
using nem.Mimir.Application.Common.Interfaces;
using NSubstitute;
using Shouldly;

namespace nem.Mimir.Api.Tests.Controllers;

public sealed class BillingControllerTests
{
    private static readonly DateTimeOffset From = new(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset To = new(2024, 2, 1, 0, 0, 0, TimeSpan.Zero);
    private const string TenantId = "tenant-abc";

    private static (BillingController controller, ITenantUsageQueryService usageService) CreateController(
        string? tenantId = TenantId)
    {
        var usageService = Substitute.For<ITenantUsageQueryService>();
        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.TenantId.Returns(tenantId);

        var controller = new BillingController(usageService, currentUserService);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext(),
        };

        return (controller, usageService);
    }

    [Fact]
    public async Task GetUsage_ReturnsOkWithTenantUsageSummary()
    {
        var (controller, usageService) = CreateController();
        var expected = new TenantUsageSummary
        {
            TenantId = TenantId,
            PeriodStart = From,
            PeriodEnd = To,
            TotalInputTokens = 1000,
            TotalOutputTokens = 500,
            TotalCost = 0.05m,
            RequestCount = 10,
        };
        usageService.GetUsageAsync(TenantId, From, To, Arg.Any<CancellationToken>())
            .Returns(expected);

        var result = await controller.GetUsage(From, To);

        var ok = result.ShouldBeOfType<OkObjectResult>();
        var summary = ok.Value.ShouldBeOfType<TenantUsageSummary>();
        summary.TenantId.ShouldBe(TenantId);
        summary.TotalInputTokens.ShouldBe(1000);
        summary.TotalOutputTokens.ShouldBe(500);
        summary.TotalCost.ShouldBe(0.05m);
        summary.RequestCount.ShouldBe(10);
    }

    [Fact]
    public async Task GetUsageByModel_ReturnsOkWithModelBreakdown()
    {
        var (controller, usageService) = CreateController();
        var summary = new TenantUsageSummary
        {
            TenantId = TenantId,
            PeriodStart = From,
            PeriodEnd = To,
            ModelBreakdown = new Dictionary<string, ModelUsage>
            {
                ["gpt-4"] = new ModelUsage
                {
                    Model = "gpt-4",
                    InputTokens = 800,
                    OutputTokens = 400,
                    Cost = 0.04m,
                    RequestCount = 8,
                },
                ["gpt-3.5-turbo"] = new ModelUsage
                {
                    Model = "gpt-3.5-turbo",
                    InputTokens = 200,
                    OutputTokens = 100,
                    Cost = 0.01m,
                    RequestCount = 2,
                },
            },
        };
        usageService.GetUsageAsync(TenantId, From, To, Arg.Any<CancellationToken>())
            .Returns(summary);

        var result = await controller.GetUsageByModel(From, To);

        var ok = result.ShouldBeOfType<OkObjectResult>();
        var breakdown = ok.Value.ShouldBeOfType<Dictionary<string, ModelUsage>>();
        breakdown.Count.ShouldBe(2);
        breakdown.ShouldContainKey("gpt-4");
        breakdown["gpt-4"].InputTokens.ShouldBe(800);
        breakdown.ShouldContainKey("gpt-3.5-turbo");
    }

    [Fact]
    public async Task GetUsage_EmptyDateRange_ReturnsEmptySummary()
    {
        var (controller, usageService) = CreateController();
        var empty = new TenantUsageSummary
        {
            TenantId = TenantId,
            PeriodStart = From,
            PeriodEnd = To,
            TotalInputTokens = 0,
            TotalOutputTokens = 0,
            TotalCost = 0m,
            RequestCount = 0,
        };
        usageService.GetUsageAsync(TenantId, From, To, Arg.Any<CancellationToken>())
            .Returns(empty);

        var result = await controller.GetUsage(From, To);

        var ok = result.ShouldBeOfType<OkObjectResult>();
        var summary = ok.Value.ShouldBeOfType<TenantUsageSummary>();
        summary.RequestCount.ShouldBe(0);
        summary.TotalCost.ShouldBe(0m);
        summary.ModelBreakdown.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetUsageByModel_EmptyDateRange_ReturnsEmptyBreakdown()
    {
        var (controller, usageService) = CreateController();
        var empty = new TenantUsageSummary
        {
            TenantId = TenantId,
            PeriodStart = From,
            PeriodEnd = To,
        };
        usageService.GetUsageAsync(TenantId, From, To, Arg.Any<CancellationToken>())
            .Returns(empty);

        var result = await controller.GetUsageByModel(From, To);

        var ok = result.ShouldBeOfType<OkObjectResult>();
        var breakdown = ok.Value.ShouldBeOfType<Dictionary<string, ModelUsage>>();
        breakdown.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetUsage_PassesTenantIdFromCurrentUser()
    {
        var (controller, usageService) = CreateController(tenantId: "user-xyz");
        usageService.GetUsageAsync(Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(new TenantUsageSummary { TenantId = "user-xyz" });

        await controller.GetUsage(From, To);

        await usageService.Received(1).GetUsageAsync("user-xyz", From, To, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetUsageByModel_PassesTenantIdFromCurrentUser()
    {
        var (controller, usageService) = CreateController(tenantId: "user-xyz");
        usageService.GetUsageAsync(Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(new TenantUsageSummary { TenantId = "user-xyz" });

        await controller.GetUsageByModel(From, To);

        await usageService.Received(1).GetUsageAsync("user-xyz", From, To, Arg.Any<CancellationToken>());
    }
}
