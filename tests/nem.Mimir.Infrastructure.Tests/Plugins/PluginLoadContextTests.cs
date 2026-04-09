using System.Runtime.Loader;
using Shouldly;
using nem.Mimir.Infrastructure.Plugins;

namespace nem.Mimir.Infrastructure.Tests.Plugins;

public sealed class PluginLoadContextTests
{
    [Fact]
    public void Load_ShouldShareNemContractsAssemblyWithHost()
    {
        var sut = new PluginLoadContext(typeof(PluginManager).Assembly.Location);

        var sharedAssembly = sut.LoadFromAssemblyName(typeof(nem.Contracts.Lifecycle.IDataSubjectContributor).Assembly.GetName());

        sharedAssembly.ShouldBeSameAs(typeof(nem.Contracts.Lifecycle.IDataSubjectContributor).Assembly);
        AssemblyLoadContext.GetLoadContext(sharedAssembly).ShouldBe(AssemblyLoadContext.Default);
    }

    [Fact]
    public void Load_ShouldIsolateNonSharedAssemblies()
    {
        var sut = new PluginLoadContext(typeof(PluginManager).Assembly.Location);

        var assembly = sut.LoadFromAssemblyName(typeof(PluginManager).Assembly.GetName());

        assembly.ShouldNotBeSameAs(typeof(PluginManager).Assembly);
        AssemblyLoadContext.GetLoadContext(assembly).ShouldBe(sut);
    }
}
