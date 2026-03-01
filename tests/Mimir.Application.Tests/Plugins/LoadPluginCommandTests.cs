using Mimir.Application.Common.Interfaces;
using Mimir.Application.Plugins.Commands;
using Mimir.Domain.Plugins;
using NSubstitute;
using Shouldly;

namespace Mimir.Application.Tests.Plugins;

public sealed class LoadPluginCommandTests
{
    private readonly IPluginService _pluginService;
    private readonly LoadPluginCommandHandler _handler;

    public LoadPluginCommandTests()
    {
        _pluginService = Substitute.For<IPluginService>();
        _handler = new LoadPluginCommandHandler(_pluginService);
    }

    [Fact]
    public async Task Handle_ValidPath_ShouldReturnMetadata()
    {
        // Arrange
        var metadata = PluginMetadata.Create("test-plugin", "Test Plugin", "1.0.0", "A test plugin");
        _pluginService.LoadPluginAsync("/plugins/test.dll", Arg.Any<CancellationToken>())
            .Returns(metadata);

        var command = new LoadPluginCommand("/plugins/test.dll");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe("test-plugin");
        result.Name.ShouldBe("Test Plugin");
        result.Version.ShouldBe("1.0.0");
        result.Description.ShouldBe("A test plugin");

        await _pluginService.Received(1).LoadPluginAsync("/plugins/test.dll", Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Validator_EmptyPath_ShouldFail()
    {
        var validator = new LoadPluginCommandValidator();
        var command = new LoadPluginCommand("");

        var result = validator.Validate(command);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "AssemblyPath");
    }

    [Fact]
    public void Validator_ValidPath_ShouldPass()
    {
        var validator = new LoadPluginCommandValidator();
        var command = new LoadPluginCommand("/plugins/test.dll");

        var result = validator.Validate(command);

        result.IsValid.ShouldBeTrue();
    }
}
