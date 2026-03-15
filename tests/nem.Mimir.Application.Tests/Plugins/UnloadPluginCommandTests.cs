using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Plugins.Commands;
using NSubstitute;
using Shouldly;

namespace nem.Mimir.Application.Tests.Plugins;

public sealed class UnloadPluginCommandTests
{
    private readonly IPluginService _pluginService;
    private readonly UnloadPluginCommandHandler _handler;

    public UnloadPluginCommandTests()
    {
        _pluginService = Substitute.For<IPluginService>();
        _handler = new UnloadPluginCommandHandler(_pluginService);
    }

    [Fact]
    public async Task Handle_ValidId_ShouldCallUnload()
    {
        // Arrange
        var command = new UnloadPluginCommand("test-plugin");

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        await _pluginService.Received(1).UnloadPluginAsync("test-plugin", Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Validator_EmptyPluginId_ShouldFail()
    {
        var validator = new UnloadPluginCommandValidator();
        var command = new UnloadPluginCommand("");

        var result = validator.Validate(command);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "PluginId");
    }

    [Fact]
    public void Validator_ValidPluginId_ShouldPass()
    {
        var validator = new UnloadPluginCommandValidator();
        var command = new UnloadPluginCommand("test-plugin");

        var result = validator.Validate(command);

        result.IsValid.ShouldBeTrue();
    }
}
