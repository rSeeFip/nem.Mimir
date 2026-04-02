using Microsoft.Extensions.DependencyInjection;
using nem.Contracts.AspNetCore.Organism.MapeK;
using nem.Mimir.Infrastructure.Organism.MapeK;
using Shouldly;

namespace nem.Mimir.Infrastructure.Tests.Organism.MapeK;

public sealed class MimirMapeKRegistrationTests
{
    [Fact]
    public void Playbooks_have_expected_count_and_unique_trigger_names()
    {
        var playbooks = MimirMapeKPlaybooks.Create();

        playbooks.Count.ShouldBe(5);
        playbooks.Select(x => x.Name).Distinct(StringComparer.Ordinal).Count().ShouldBe(playbooks.Count);
        playbooks.Select(x => x.Trigger).Distinct(StringComparer.Ordinal).Count().ShouldBe(playbooks.Count);
    }

    [Fact]
    public void Registration_adds_mapek_agent_and_custom_planner()
    {
        var services = new ServiceCollection();

        services.AddMimirMapeK();

        services.Any(sd => sd.ServiceType == typeof(IMapeKLoop)).ShouldBeTrue();
        services.Any(sd => sd.ServiceType == typeof(MapeKConfigAgent)).ShouldBeTrue();
        services.Any(sd => sd.ServiceType == typeof(MapeKPlanner)).ShouldBeTrue();
    }
}
