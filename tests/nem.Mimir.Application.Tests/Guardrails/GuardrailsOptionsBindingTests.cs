using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using nem.Mimir.Application.Guardrails;
using nem.Mimir.Application.Tokens;
using Shouldly;

namespace nem.Mimir.Application.Tests.Guardrails;

public sealed class GuardrailsOptionsBindingTests
{
    [Fact]
    public void ApplicationServices_BindTokenAndGuardrailOptions_FromConfiguration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{TokenBudgetGovernorOptions.SectionName}:DefaultBudget"] = "7000",
                [$"{TokenBudgetGovernorOptions.SectionName}:WarnThresholdPercent"] = "80",
                [$"{GuardrailsOptions.SectionName}:Enabled"] = "true",
                [$"{GuardrailsOptions.SectionName}:MaxOutputTokens"] = "4096",
                [$"{GuardrailsOptions.SectionName}:ActiveBundle"] = "strict",
                [$"{GuardrailsOptions.SectionName}:AvailableBundles:0"] = "permissive",
                [$"{GuardrailsOptions.SectionName}:AvailableBundles:1"] = "standard",
                [$"{GuardrailsOptions.SectionName}:AvailableBundles:2"] = "strict",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddApplicationServices(configuration);

        using var provider = services.BuildServiceProvider();

        var tokenOptions = provider.GetRequiredService<IOptions<TokenBudgetGovernorOptions>>().Value;
        var guardrailsOptions = provider.GetRequiredService<IOptions<GuardrailsOptions>>().Value;

        tokenOptions.DefaultBudget.ShouldBe(7000);
        tokenOptions.WarnThresholdPercent.ShouldBe(80);
        guardrailsOptions.Enabled.ShouldBeTrue();
        guardrailsOptions.MaxOutputTokens.ShouldBe(4096);
        guardrailsOptions.ActiveBundle.ShouldBe("strict");
        guardrailsOptions.AvailableBundles.ShouldBe(["permissive", "standard", "strict"]);
    }
}
