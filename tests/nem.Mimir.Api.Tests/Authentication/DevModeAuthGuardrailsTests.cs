using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using nem.Contracts.AspNetCore.Auth;
using Shouldly;

namespace nem.Mimir.Api.Tests.Authentication;

public sealed class DevModeAuthGuardrailsTests
{
    [Fact]
    public void ProductionEnvironment_ShouldBlockDevModeAuthIfRequested()
    {
        var environment = new TestHostEnvironment(Environments.Production);
        var warnings = new List<string>();

        var isActive = NemDevModeGuardrailsExtensions.IsNemDevModeAuthActive(environment, requestedByConfiguration: true, warnings.Add);

        isActive.ShouldBeFalse();
        warnings.Count.ShouldBe(1);
    }

    [Fact]
    public void DevelopmentEnvironment_ShouldAllowDevModeAuth_WithWarning()
    {
        var environment = new TestHostEnvironment(Environments.Development);
        var warnings = new List<string>();

        var isActive = NemDevModeGuardrailsExtensions.IsNemDevModeAuthActive(environment, requestedByConfiguration: true, warnings.Add);

        isActive.ShouldBeTrue();
        warnings.Count.ShouldBe(1);
        warnings[0].ShouldContain("Dev-mode authentication is active. This MUST NOT be used in production.");
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "nem.Mimir.Api.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
