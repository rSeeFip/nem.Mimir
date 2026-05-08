using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Tenants.Commands;
using nem.Mimir.Domain.Tenants;
using NSubstitute;
using Shouldly;

namespace nem.Mimir.Application.Tests.Tenants;

public sealed class TenantLifecycleHandlerTests
{
    private readonly ITenantStore _tenantStore = Substitute.For<ITenantStore>();
    private readonly IDateTimeService _dateTimeService = Substitute.For<IDateTimeService>();

    [Fact]
    public async Task OnboardTenant_CreatesTenantWithDefaultConfiguration()
    {
        _dateTimeService.UtcNow.Returns(new DateTimeOffset(2026, 5, 7, 12, 0, 0, TimeSpan.Zero));
        _tenantStore.ExistsBySlugAsync("acme", Arg.Any<CancellationToken>()).Returns(false);
        var handler = new OnboardTenantCommandHandler(_tenantStore, _dateTimeService);

        var result = await handler.Handle(new OnboardTenantCommand("Acme", null, null), CancellationToken.None);

        result.Name.ShouldBe("Acme");
        result.Slug.ShouldBe("acme");
        result.DefaultRateLimit.ShouldBe(100);
        result.Status.ShouldBe(TenantStatus.Active);

        await _tenantStore.Received(1).AddAsync(
            Arg.Is<Tenant>(tenant =>
                tenant.Name == "Acme" &&
                tenant.Slug == "acme" &&
                tenant.DefaultRateLimit == 100 &&
                tenant.Status == TenantStatus.Active),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OffboardTenant_SoftDeletesAndSetsThirtyDayRetention()
    {
        var now = new DateTimeOffset(2026, 5, 7, 12, 0, 0, TimeSpan.Zero);
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Acme",
            Slug = "acme",
            Status = TenantStatus.Active,
            DefaultRateLimit = 100,
            CreatedAt = now.AddDays(-7),
        };

        _dateTimeService.UtcNow.Returns(now);
        _tenantStore.GetByIdAsync(tenant.Id, Arg.Any<CancellationToken>()).Returns(tenant);
        var handler = new OffboardTenantCommandHandler(_tenantStore, _dateTimeService);

        await handler.Handle(new OffboardTenantCommand(tenant.Id), CancellationToken.None);

        tenant.Status.ShouldBe(TenantStatus.Offboarded);
        tenant.OffboardedAt.ShouldBe(now);
        tenant.DataRetentionUntil.ShouldBe(now.AddDays(30));

        await _tenantStore.Received(1).UpdateAsync(
            Arg.Is<Tenant>(x =>
                x.Status == TenantStatus.Offboarded &&
                x.OffboardedAt == now &&
                x.DataRetentionUntil == now.AddDays(30)),
            Arg.Any<CancellationToken>());
    }
}
