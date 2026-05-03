using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using nem.Mimir.Application.Common.Sanitization;
using Shouldly;

namespace nem.Mimir.Application.Tests.Common.Sanitization;

public sealed class SanitizationOptionsTests
{
    [Fact]
    public void BindsOptionsFromConfiguration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{SanitizationOptions.SectionName}:DefaultMode"] = "Log",
                [$"{SanitizationOptions.SectionName}:ChannelOverrides:telegram"] = "Block",
                [$"{SanitizationOptions.SectionName}:ChannelOverrides:teams"] = "Sanitize"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddOptions<SanitizationOptions>()
            .BindConfiguration(SanitizationOptions.SectionName);

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<SanitizationOptions>>().Value;

        options.DefaultMode.ShouldBe(SanitizationMode.Log);
        options.ChannelOverrides["telegram"].ShouldBe(SanitizationMode.Block);
        options.ChannelOverrides["teams"].ShouldBe(SanitizationMode.Sanitize);
    }

    [Fact]
    public void GetModeForChannel_FallsBackToDefaultMode_WhenChannelMissing()
    {
        var options = new SanitizationOptions
        {
            DefaultMode = SanitizationMode.Log
        };

        options.GetModeForChannel("unknown").ShouldBe(SanitizationMode.Log);
    }

    [Fact]
    public void GetModeForChannel_ReturnsOverride_WhenChannelConfigured()
    {
        var options = new SanitizationOptions
        {
            DefaultMode = SanitizationMode.Sanitize,
            ChannelOverrides = new Dictionary<string, SanitizationMode>(StringComparer.OrdinalIgnoreCase)
            {
                ["telegram"] = SanitizationMode.Block
            }
        };

        options.GetModeForChannel("telegram").ShouldBe(SanitizationMode.Block);
    }
}
