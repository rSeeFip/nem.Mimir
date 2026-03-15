using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Plugins.Commands;
using nem.Mimir.Domain.Plugins;
using NSubstitute;
using Shouldly;

namespace nem.Mimir.Application.Tests.Plugins;

public sealed class ExecutePluginCommandTests
{
    private readonly IPluginService _pluginService;
    private readonly ExecutePluginCommandHandler _handler;

    public ExecutePluginCommandTests()
    {
        _pluginService = Substitute.For<IPluginService>();
        _handler = new ExecutePluginCommandHandler(_pluginService);
    }

    [Fact]
    public async Task Handle_ValidCommand_ShouldReturnResult()
    {
        // Arrange
        var expectedResult = PluginResult.Success(
            new Dictionary<string, object> { ["output"] = "hello" });

        _pluginService.ExecutePluginAsync(
                "test-plugin",
                Arg.Any<PluginContext>(),
                Arg.Any<CancellationToken>())
            .Returns(expectedResult);

        var parameters = new Dictionary<string, object> { ["input"] = "world" };
        var command = new ExecutePluginCommand("test-plugin", "user-123", parameters);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeTrue();
        result.Data["output"].ShouldBe("hello");

        await _pluginService.Received(1).ExecutePluginAsync(
            "test-plugin",
            Arg.Is<PluginContext>(ctx => ctx.UserId == "user-123"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Validator_EmptyPluginId_ShouldFail()
    {
        var validator = new ExecutePluginCommandValidator();
        var command = new ExecutePluginCommand("", "user-123", new Dictionary<string, object>());

        var result = validator.Validate(command);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "PluginId");
    }

    [Fact]
    public void Validator_EmptyUserId_ShouldFail()
    {
        var validator = new ExecutePluginCommandValidator();
        var command = new ExecutePluginCommand("test-plugin", "", new Dictionary<string, object>());

        var result = validator.Validate(command);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "UserId");
    }

    [Fact]
    public void Validator_ValidCommand_ShouldPass()
    {
        var validator = new ExecutePluginCommandValidator();
        var command = new ExecutePluginCommand("test-plugin", "user-123", new Dictionary<string, object>());

        var result = validator.Validate(command);

        result.IsValid.ShouldBeTrue();
    }
}
