using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using nem.Mimir.Infrastructure;
using nem.Mimir.Infrastructure.Inference;
using Shouldly;

namespace nem.Mimir.Infrastructure.Tests.Inference;

public sealed class InferencePolicyOptionsBindingTests
{
    [Fact]
    public void InfrastructureServices_BindInferencePolicyOptions_FromConfiguration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{InferencePolicyOptions.SectionName}:Enabled"] = "true",
                [$"{InferencePolicyOptions.SectionName}:DefaultPolicy"] = "allow-all",
                [$"{InferencePolicyOptions.SectionName}:Action"] = "Redirect",
                [$"{InferencePolicyOptions.SectionName}:RedirectAlias"] = "fast",
            })
            .Build();

        var services = new ServiceCollection();
        services.Configure<InferencePolicyOptions>(configuration.GetSection(InferencePolicyOptions.SectionName));

        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<InferencePolicyOptions>>().Value;

        options.Enabled.ShouldBeTrue();
        options.DefaultPolicy.ShouldBe("allow-all");
        options.Action.ShouldBe(nem.Contracts.Inference.PolicyAction.Redirect);
        options.RedirectAlias.ShouldBe("fast");
    }
}
