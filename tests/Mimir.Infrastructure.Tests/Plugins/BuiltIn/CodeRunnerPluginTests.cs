using Microsoft.Extensions.DependencyInjection;
using Mimir.Application.Common.Interfaces;
using Mimir.Application.Common.Models;
using Mimir.Domain.Plugins;
using Mimir.Infrastructure.Plugins.BuiltIn;
using NSubstitute;
using Shouldly;

namespace Mimir.Infrastructure.Tests.Plugins.BuiltIn;

public sealed class CodeRunnerPluginTests
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ISandboxService _sandboxService;
    private readonly CodeRunnerPlugin _sut;

    public CodeRunnerPluginTests()
    {
        _sandboxService = Substitute.For<ISandboxService>();

        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(ISandboxService)).Returns(_sandboxService);

        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(serviceProvider);

        _scopeFactory = Substitute.For<IServiceScopeFactory>();
        _scopeFactory.CreateScope().Returns(scope);

        _sut = new CodeRunnerPlugin(_scopeFactory);
    }

    [Fact]
    public void Metadata_ShouldHaveCorrectValues()
    {
        _sut.Id.ShouldBe("mimir.builtin.code-runner");
        _sut.Name.ShouldBe("Code Runner");
        _sut.Version.ShouldBe("1.0.0");
        _sut.Description.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ExecuteAsync_WithValidCode_ReturnsSuccess()
    {
        // Arrange
        var context = PluginContext.Create("user-1", new Dictionary<string, object>
        {
            ["code"] = "print('hello')",
            ["language"] = "python",
        });
        _sandboxService.ExecuteAsync("print('hello')", "python", Arg.Any<CancellationToken>())
            .Returns(new CodeExecutionResult("hello\n", "", 0, 150, false));

        // Act
        var result = await _sut.ExecuteAsync(context);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Data["stdout"].ShouldBe("hello\n");
        result.Data["stderr"].ShouldBe("");
        result.Data["exitCode"].ShouldBe(0);
        result.Data["executionTimeMs"].ShouldBe(150L);
    }

    [Fact]
    public async Task ExecuteAsync_WithMissingCodeParam_ReturnsFailure()
    {
        var context = PluginContext.Create("user-1", new Dictionary<string, object>());

        var result = await _sut.ExecuteAsync(context);

        result.IsSuccess.ShouldBeFalse();
        result.ErrorMessage!.ShouldContain("code");
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyCodeParam_ReturnsFailure()
    {
        var context = PluginContext.Create("user-1", new Dictionary<string, object>
        {
            ["code"] = "   ",
        });

        var result = await _sut.ExecuteAsync(context);

        result.IsSuccess.ShouldBeFalse();
        result.ErrorMessage!.ShouldContain("code");
    }

    [Fact]
    public async Task ExecuteAsync_WithUnsupportedLanguage_ReturnsFailure()
    {
        var context = PluginContext.Create("user-1", new Dictionary<string, object>
        {
            ["code"] = "puts 'hi'",
            ["language"] = "ruby",
        });

        var result = await _sut.ExecuteAsync(context);

        result.IsSuccess.ShouldBeFalse();
        result.ErrorMessage!.ShouldContain("ruby");
        result.ErrorMessage!.ShouldContain("Unsupported language");
    }

    [Fact]
    public async Task ExecuteAsync_DefaultsToPython_WhenLanguageNotSpecified()
    {
        // Arrange
        var context = PluginContext.Create("user-1", new Dictionary<string, object>
        {
            ["code"] = "print(1)",
        });
        _sandboxService.ExecuteAsync("print(1)", "python", Arg.Any<CancellationToken>())
            .Returns(new CodeExecutionResult("1\n", "", 0, 50, false));

        // Act
        var result = await _sut.ExecuteAsync(context);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        await _sandboxService.Received(1).ExecuteAsync("print(1)", "python", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithJavaScript_CallsSandboxCorrectly()
    {
        // Arrange
        var context = PluginContext.Create("user-1", new Dictionary<string, object>
        {
            ["code"] = "console.log('hi')",
            ["language"] = "javascript",
        });
        _sandboxService.ExecuteAsync("console.log('hi')", "javascript", Arg.Any<CancellationToken>())
            .Returns(new CodeExecutionResult("hi\n", "", 0, 80, false));

        // Act
        var result = await _sut.ExecuteAsync(context);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        await _sandboxService.Received(1).ExecuteAsync("console.log('hi')", "javascript", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithTimeout_ReturnsFailure()
    {
        // Arrange
        var context = PluginContext.Create("user-1", new Dictionary<string, object>
        {
            ["code"] = "while True: pass",
        });
        _sandboxService.ExecuteAsync("while True: pass", "python", Arg.Any<CancellationToken>())
            .Returns(new CodeExecutionResult("", "", 137, 30000, true));

        // Act
        var result = await _sut.ExecuteAsync(context);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.ErrorMessage!.ShouldContain("timed out");
    }

    [Fact]
    public async Task ExecuteAsync_WithNonZeroExitCode_ReturnsFailure()
    {
        // Arrange
        var context = PluginContext.Create("user-1", new Dictionary<string, object>
        {
            ["code"] = "raise Exception('boom')",
        });
        _sandboxService.ExecuteAsync("raise Exception('boom')", "python", Arg.Any<CancellationToken>())
            .Returns(new CodeExecutionResult("", "Traceback: Exception: boom", 1, 100, false));

        // Act
        var result = await _sut.ExecuteAsync(context);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.ErrorMessage!.ShouldContain("exit code 1");
        result.ErrorMessage!.ShouldContain("boom");
    }

    [Fact]
    public async Task InitializeAsync_CompletesSuccessfully()
    {
        await _sut.InitializeAsync();
        // Should not throw
    }

    [Fact]
    public async Task ShutdownAsync_CompletesSuccessfully()
    {
        await _sut.ShutdownAsync();
        // Should not throw
    }

    [Fact]
    public async Task ExecuteAsync_WithNonStringCodeParam_ReturnsFailure()
    {
        var context = PluginContext.Create("user-1", new Dictionary<string, object>
        {
            ["code"] = 42,
        });

        var result = await _sut.ExecuteAsync(context);

        result.IsSuccess.ShouldBeFalse();
        result.ErrorMessage!.ShouldContain("code");
    }
}
