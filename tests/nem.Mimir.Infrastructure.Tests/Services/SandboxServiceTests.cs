using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.Extensions.Logging;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Infrastructure.Services;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;

namespace nem.Mimir.Infrastructure.Tests.Services;

#pragma warning disable CS0618
public sealed class SandboxServiceTests
{
    private readonly IDockerClient _dockerClient = Substitute.For<IDockerClient>();
    private readonly IContainerOperations _containerOps = Substitute.For<IContainerOperations>();
    private readonly ILogger<SandboxService> _logger = Substitute.For<ILogger<SandboxService>>();

    private readonly SandboxService _sut;

    public SandboxServiceTests()
    {
        _dockerClient.Containers.Returns(_containerOps);
        _sut = new SandboxService(_dockerClient, _logger);
    }

    private void SetupSuccessfulExecution(string containerId = "test-container-123", long exitCode = 0)
    {
        _containerOps
            .CreateContainerAsync(Arg.Any<CreateContainerParameters>(), Arg.Any<CancellationToken>())
            .Returns(new CreateContainerResponse { ID = containerId });

        _containerOps
            .ExtractArchiveToContainerAsync(containerId, Arg.Any<CopyToContainerParameters>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        _containerOps
    .StartContainerAsync(containerId, Arg.Any<ContainerStartParameters>(), Arg.Any<CancellationToken>())
    .Returns(true);

        _containerOps
            .WaitContainerAsync(containerId, Arg.Any<CancellationToken>())
            .Returns(new ContainerWaitResponse { StatusCode = exitCode });

        _containerOps
            .GetContainerLogsAsync(containerId, Arg.Any<ContainerLogsParameters>(), Arg.Any<IProgress<string>>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        _containerOps
            .RemoveContainerAsync(containerId, Arg.Any<ContainerRemoveParameters>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
    }

    // ── Argument Validation ─────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_NullCode_ThrowsArgumentException()
    {
        await Should.ThrowAsync<ArgumentException>(
            () => _sut.ExecuteAsync(null!, "python"));
    }

    [Fact]
    public async Task ExecuteAsync_EmptyCode_ThrowsArgumentException()
    {
        await Should.ThrowAsync<ArgumentException>(
            () => _sut.ExecuteAsync("", "python"));
    }

    [Fact]
    public async Task ExecuteAsync_WhitespaceCode_ThrowsArgumentException()
    {
        await Should.ThrowAsync<ArgumentException>(
            () => _sut.ExecuteAsync("   ", "python"));
    }

    [Fact]
    public async Task ExecuteAsync_NullLanguage_ThrowsArgumentException()
    {
        await Should.ThrowAsync<ArgumentException>(
            () => _sut.ExecuteAsync("print('hi')", null!));
    }

    [Fact]
    public async Task ExecuteAsync_UnsupportedLanguage_ThrowsArgumentException()
    {
        var ex = await Should.ThrowAsync<ArgumentException>(
            () => _sut.ExecuteAsync("fn main() {}", "rust"));

        ex.Message.ShouldContain("Unsupported language");
        ex.Message.ShouldContain("rust");
    }

    // ── Container Creation ──────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_Python_CreatesContainerWithCorrectCommand()
    {
        SetupSuccessfulExecution();

        await _sut.ExecuteAsync("print('hello')", "python");

        await _containerOps.Received(1).CreateContainerAsync(
            Arg.Is<CreateContainerParameters>(p =>
                p.Cmd != null &&
                p.Cmd.Count == 2 &&
                p.Cmd[0] == "python3" &&
                p.Cmd[1] == "/sandbox/code.py"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_JavaScript_CreatesContainerWithCorrectCommand()
    {
        SetupSuccessfulExecution();

        await _sut.ExecuteAsync("console.log('hello')", "javascript");

        await _containerOps.Received(1).CreateContainerAsync(
            Arg.Is<CreateContainerParameters>(p =>
                p.Cmd != null &&
                p.Cmd.Count == 2 &&
                p.Cmd[0] == "node" &&
                p.Cmd[1] == "/sandbox/code.js"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_CreatesContainerWithSecurityConstraints()
    {
        SetupSuccessfulExecution();

        await _sut.ExecuteAsync("print('hello')", "python");

        await _containerOps.Received(1).CreateContainerAsync(
            Arg.Is<CreateContainerParameters>(p =>
                p.Image == "mimir-sandbox:latest" &&
                p.NetworkDisabled == true &&
                p.User == "sandbox" &&
                p.WorkingDir == "/sandbox" &&
                p.HostConfig != null &&
                p.HostConfig.ReadonlyRootfs == true &&
                p.HostConfig.Memory == 512L * 1024 * 1024 &&
                p.HostConfig.NanoCPUs == 1_000_000_000 &&
                p.HostConfig.Tmpfs != null &&
                p.HostConfig.Tmpfs.ContainsKey("/tmp") &&
                p.HostConfig.CapDrop != null &&
                p.HostConfig.CapDrop.Contains("ALL")),
            Arg.Any<CancellationToken>());
    }

    // ── Successful Execution ────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_SuccessfulExecution_ReturnsExitCodeZero()
    {
        SetupSuccessfulExecution(exitCode: 0);

        var result = await _sut.ExecuteAsync("print('hello')", "python");

        result.ExitCode.ShouldBe(0);
        result.TimedOut.ShouldBeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_SuccessfulExecution_ReturnsExecutionTime()
    {
        SetupSuccessfulExecution();

        var result = await _sut.ExecuteAsync("print('hello')", "python");

        result.ExecutionTimeMs.ShouldBeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task ExecuteAsync_NonZeroExitCode_ReturnsExitCode()
    {
        SetupSuccessfulExecution(exitCode: 1);

        var result = await _sut.ExecuteAsync("import sys; sys.exit(1)", "python");

        result.ExitCode.ShouldBe(1);
        result.TimedOut.ShouldBeFalse();
    }

    // ── Container Lifecycle ─────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_CopiesCodeToContainer()
    {
        SetupSuccessfulExecution();

        await _sut.ExecuteAsync("print('hello')", "python");

        await _containerOps.Received(1).ExtractArchiveToContainerAsync(
            "test-container-123",
            Arg.Is<CopyToContainerParameters>(p => p.Path == "/sandbox"),
            Arg.Any<Stream>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_StartsContainer()
    {
        SetupSuccessfulExecution();

        await _sut.ExecuteAsync("print('hello')", "python");

        await _containerOps.Received(1).StartContainerAsync(
            "test-container-123",
            Arg.Any<ContainerStartParameters>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_AlwaysRemovesContainerOnSuccess()
    {
        SetupSuccessfulExecution();

        await _sut.ExecuteAsync("print('hello')", "python");

        await _containerOps.Received(1).RemoveContainerAsync(
            "test-container-123",
            Arg.Is<ContainerRemoveParameters>(p => p.Force == true),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_AlwaysRemovesContainerOnError()
    {
        const string containerId = "error-container-456";

        _containerOps
            .CreateContainerAsync(Arg.Any<CreateContainerParameters>(), Arg.Any<CancellationToken>())
            .Returns(new CreateContainerResponse { ID = containerId });

        _containerOps
            .ExtractArchiveToContainerAsync(containerId, Arg.Any<CopyToContainerParameters>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        _containerOps
            .StartContainerAsync(containerId, Arg.Any<ContainerStartParameters>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new DockerApiException(System.Net.HttpStatusCode.InternalServerError, "Docker error"));

        _containerOps
            .RemoveContainerAsync(containerId, Arg.Any<ContainerRemoveParameters>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        await Should.ThrowAsync<DockerApiException>(
            () => _sut.ExecuteAsync("print('hello')", "python"));

        await _containerOps.Received(1).RemoveContainerAsync(
            containerId,
            Arg.Is<ContainerRemoveParameters>(p => p.Force == true),
            Arg.Any<CancellationToken>());
    }

    // ── Timeout ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_Timeout_ReturnsTimedOutResult()
    {
        const string containerId = "timeout-container-789";

        _containerOps
            .CreateContainerAsync(Arg.Any<CreateContainerParameters>(), Arg.Any<CancellationToken>())
            .Returns(new CreateContainerResponse { ID = containerId });

        _containerOps
            .ExtractArchiveToContainerAsync(containerId, Arg.Any<CopyToContainerParameters>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        _containerOps
            .StartContainerAsync(containerId, Arg.Any<ContainerStartParameters>(), Arg.Any<CancellationToken>())
            .Returns(true);

        // Simulate the container never finishing — WaitContainerAsync blocks until cancellation
        _containerOps
            .WaitContainerAsync(containerId, Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                var ct = callInfo.ArgAt<CancellationToken>(1);
                await Task.Delay(TimeSpan.FromMinutes(5), ct);
                return new ContainerWaitResponse { StatusCode = 0 };
            });

        _containerOps
            .KillContainerAsync(containerId, Arg.Any<ContainerKillParameters>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        _containerOps
            .GetContainerLogsAsync(containerId, Arg.Any<ContainerLogsParameters>(), Arg.Any<IProgress<string>>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        _containerOps
            .RemoveContainerAsync(containerId, Arg.Any<ContainerRemoveParameters>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var result = await _sut.ExecuteAsync("while True: pass", "python");

        result.TimedOut.ShouldBeTrue();
        result.ExitCode.ShouldBe(137); // SIGKILL exit code

        await _containerOps.Received(1).KillContainerAsync(
            containerId,
            Arg.Is<ContainerKillParameters>(p => p.Signal == "SIGKILL"),
            Arg.Any<CancellationToken>());
    }

    // ── Language Case-Insensitive ───────────────────────────────────────

    [Theory]
    [InlineData("Python")]
    [InlineData("PYTHON")]
    [InlineData("python")]
    [InlineData("JavaScript")]
    [InlineData("JAVASCRIPT")]
    [InlineData("javascript")]
    public async Task ExecuteAsync_LanguageIsCaseInsensitive(string language)
    {
        SetupSuccessfulExecution();

        var result = await _sut.ExecuteAsync("print('hello')", language);

        result.ShouldNotBeNull();
        result.TimedOut.ShouldBeFalse();
    }

    // ── Cancellation ────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        _containerOps
            .CreateContainerAsync(Arg.Any<CreateContainerParameters>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(callInfo =>
            {
                callInfo.ArgAt<CancellationToken>(1).ThrowIfCancellationRequested();
                return new OperationCanceledException();
            });

        await Should.ThrowAsync<OperationCanceledException>(
            () => _sut.ExecuteAsync("print('hello')", "python", cts.Token));
    }

    // ── Remove Container Failure ────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_RemoveContainerFails_DoesNotThrow()
    {
        const string containerId = "stubborn-container";

        _containerOps
            .CreateContainerAsync(Arg.Any<CreateContainerParameters>(), Arg.Any<CancellationToken>())
            .Returns(new CreateContainerResponse { ID = containerId });

        _containerOps
            .ExtractArchiveToContainerAsync(containerId, Arg.Any<CopyToContainerParameters>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        _containerOps
            .StartContainerAsync(containerId, Arg.Any<ContainerStartParameters>(), Arg.Any<CancellationToken>())
            .Returns(true);

        _containerOps
            .WaitContainerAsync(containerId, Arg.Any<CancellationToken>())
            .Returns(new ContainerWaitResponse { StatusCode = 0 });

        _containerOps
            .GetContainerLogsAsync(containerId, Arg.Any<ContainerLogsParameters>(), Arg.Any<IProgress<string>>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Removal fails
        _containerOps
            .RemoveContainerAsync(containerId, Arg.Any<ContainerRemoveParameters>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new DockerApiException(System.Net.HttpStatusCode.InternalServerError, "Remove failed"));

        // Should NOT throw despite removal failure
        var result = await _sut.ExecuteAsync("print('hello')", "python");

        result.ExitCode.ShouldBe(0);
    }
}
#pragma warning restore CS0618
