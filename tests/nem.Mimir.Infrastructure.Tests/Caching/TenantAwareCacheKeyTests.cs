using nem.Mimir.Infrastructure.Caching;
using Shouldly;

namespace nem.Mimir.Infrastructure.Tests.Caching;

public sealed class TenantAwareCacheKeyTests
{
    [Fact]
    public void Build_ReturnsExpectedFormat()
    {
        var key = TenantAwareCacheKey.Build("tenant-a", "models");

        key.ShouldBe("tenant:tenant-a:models");
    }

    [Fact]
    public void Build_TenantA_DiffersFromTenantB()
    {
        var keyA = TenantAwareCacheKey.Build("tenant-a", "models");
        var keyB = TenantAwareCacheKey.Build("tenant-b", "models");

        keyA.ShouldNotBe(keyB);
    }

    [Fact]
    public void Build_SameKeyDifferentTenants_AreNotEqual()
    {
        var key1 = TenantAwareCacheKey.Build("acme", TenantAwareCacheKey.Keys.Models);
        var key2 = TenantAwareCacheKey.Build("globex", TenantAwareCacheKey.Keys.Models);

        key1.ShouldNotBe(key2);
    }

    [Fact]
    public void Build_WellKnownKeys_HaveExpectedValues()
    {
        TenantAwareCacheKey.Keys.Models.ShouldBe("models");
        TenantAwareCacheKey.Keys.TenantConfig.ShouldBe("tenant-config");
        TenantAwareCacheKey.Keys.BillingSummary.ShouldBe("billing-summary");
    }

    [Theory]
    [InlineData("tenant-a", "models", "tenant:tenant-a:models")]
    [InlineData("tenant-b", "billing-summary", "tenant:tenant-b:billing-summary")]
    [InlineData("acme-corp", "tenant-config", "tenant:acme-corp:tenant-config")]
    public void Build_VariousInputs_ProduceCorrectKeys(string tenantId, string key, string expected)
    {
        TenantAwareCacheKey.Build(tenantId, key).ShouldBe(expected);
    }

    [Fact]
    public void Build_NullTenantId_Throws()
    {
        Should.Throw<ArgumentException>(() => TenantAwareCacheKey.Build(null!, "models"));
    }

    [Fact]
    public void Build_EmptyTenantId_Throws()
    {
        Should.Throw<ArgumentException>(() => TenantAwareCacheKey.Build("", "models"));
    }

    [Fact]
    public void Build_WhitespaceTenantId_Throws()
    {
        Should.Throw<ArgumentException>(() => TenantAwareCacheKey.Build("   ", "models"));
    }

    [Fact]
    public void Build_EmptyKey_Throws()
    {
        Should.Throw<ArgumentException>(() => TenantAwareCacheKey.Build("tenant-a", ""));
    }
}
