using Mimir.Application.Common.Interfaces;
using Mimir.Application.Plugins.Queries;
using Mimir.Domain.Plugins;
using NSubstitute;
using Shouldly;

namespace Mimir.Application.Tests.Plugins;

public sealed class ListPluginsQueryTests
{
    private readonly IPluginService _pluginService;
    private readonly ListPluginsQueryHandler _handler;

    public ListPluginsQueryTests()
    {
        _pluginService = Substitute.For<IPluginService>();
        _handler = new ListPluginsQueryHandler(_pluginService);
    }

    [Fact]
    public async Task Handle_ShouldReturnAllLoadedPlugins()
    {
        // Arrange
        var plugins = new List<PluginMetadata>
        {
            PluginMetadata.Create("plugin-a", "Plugin A", "1.0.0", "First"),
            PluginMetadata.Create("plugin-b", "Plugin B", "2.0.0", "Second"),
        };

        _pluginService.ListPluginsAsync(Arg.Any<CancellationToken>())
            .Returns(plugins.AsReadOnly());

        var query = new ListPluginsQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(2);
        result[0].Id.ShouldBe("plugin-a");
        result[1].Id.ShouldBe("plugin-b");

        await _pluginService.Received(1).ListPluginsAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NoPlugins_ShouldReturnEmptyList()
    {
        // Arrange
        _pluginService.ListPluginsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<PluginMetadata>().AsReadOnly());

        var query = new ListPluginsQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeEmpty();
    }
}
