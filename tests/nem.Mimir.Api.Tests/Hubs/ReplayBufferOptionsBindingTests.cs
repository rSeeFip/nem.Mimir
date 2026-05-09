using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using nem.Mimir.Api.Hubs;
using Shouldly;

namespace nem.Mimir.Api.Tests.Hubs;

public sealed class ReplayBufferOptionsBindingTests
{
    [Fact]
    public void ApiServices_BindReplayBufferOptions_FromConfiguration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{ReplayBufferOptions.SectionName}:MaxMessages"] = "50",
                [$"{ReplayBufferOptions.SectionName}:WindowMinutes"] = "5",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton(TimeProvider.System);
        services.Configure<ReplayBufferOptions>(configuration.GetSection(ReplayBufferOptions.SectionName));

        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<ReplayBufferOptions>>().Value;

        options.MaxMessages.ShouldBe(50);
        options.WindowMinutes.ShouldBe(5);
    }
}
