using System.Diagnostics;
using System.Formats.Tar;
using System.Text;
using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.Extensions.Logging;
using Mimir.Application.Common.Interfaces;
using Mimir.Application.Common.Models;

namespace Mimir.Infrastructure.Services;

/// <summary>
/// Executes code in isolated Docker sandbox containers using the Docker.DotNet SDK.
/// Each execution creates a one-shot container with strict resource limits, captures output,
/// and removes the container after completion.
/// </summary>
internal sealed class SandboxService : ISandboxService
{
    private const string SandboxImage = "mimir-sandbox:latest";
    private const long MemoryLimitBytes = 512L * 1024 * 1024; // 512 MB
    private const long NanoCpuLimit = 1_000_000_000; // 1 CPU
    private const int TimeoutSeconds = 30;
    private const string TmpfsSizeLimit = "size=104857600"; // 100 MB
    private const string SandboxWorkDir = "/sandbox";

    private static readonly IReadOnlyDictionary<string, string[]> LanguageCommands =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["python"] = ["python3", "/sandbox/code.py"],
            ["javascript"] = ["node", "/sandbox/code.js"],
        };

    private static readonly IReadOnlyDictionary<string, string> LanguageFileExtensions =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["python"] = "code.py",
            ["javascript"] = "code.js",
        };

    private readonly IDockerClient _dockerClient;
    private readonly ILogger<SandboxService> _logger;

    public SandboxService(IDockerClient dockerClient, ILogger<SandboxService> logger)
    {
        _dockerClient = dockerClient;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<CodeExecutionResult> ExecuteAsync(
        string code,
        string language,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(language);

        if (!LanguageCommands.TryGetValue(language, out var command))
        {
            throw new ArgumentException($"Unsupported language: {language}. Supported: {string.Join(", ", LanguageCommands.Keys)}", nameof(language));
        }

        var fileName = LanguageFileExtensions[language];
        string? containerId = null;
        var stopwatch = Stopwatch.StartNew();

        try
        {
            containerId = await CreateContainerAsync(command, ct);
            _logger.LogDebug("Created sandbox container {ContainerId} for {Language}", containerId, language);

            await CopyCodeToContainerAsync(containerId, fileName, code, ct);
            _logger.LogDebug("Copied code to container {ContainerId}", containerId);

            await _dockerClient.Containers.StartContainerAsync(
                containerId,
                new ContainerStartParameters(),
                ct);

            var timedOut = false;
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(TimeoutSeconds));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

            try
            {
                var waitResponse = await _dockerClient.Containers.WaitContainerAsync(
                    containerId,
                    linkedCts.Token);

                stopwatch.Stop();

                var (stdout, stderr) = await GetContainerLogsAsync(containerId, ct);

                return new CodeExecutionResult(
                    Stdout: stdout,
                    Stderr: stderr,
                    ExitCode: (int)waitResponse.StatusCode,
                    ExecutionTimeMs: stopwatch.ElapsedMilliseconds,
                    TimedOut: false);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested)
            {
                timedOut = true;
                stopwatch.Stop();

                _logger.LogWarning("Sandbox container {ContainerId} timed out after {TimeoutSeconds}s", containerId, TimeoutSeconds);

                await _dockerClient.Containers.KillContainerAsync(
                    containerId,
                    new ContainerKillParameters { Signal = "SIGKILL" },
                    ct);

                var (stdout, stderr) = await GetContainerLogsAsync(containerId, ct);

                return new CodeExecutionResult(
                    Stdout: stdout,
                    Stderr: stderr,
                    ExitCode: 137, // SIGKILL exit code
                    ExecutionTimeMs: stopwatch.ElapsedMilliseconds,
                    TimedOut: timedOut);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            stopwatch.Stop();
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not ArgumentException) // Intentional catch-all: Docker API can throw various exception types; logs and re-throws for caller handling
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error executing code in sandbox container {ContainerId}", containerId);
            throw;
        }
        finally
        {
            if (containerId is not null)
            {
                await RemoveContainerSafelyAsync(containerId);
            }
        }
    }

    private async Task<string> CreateContainerAsync(string[] command, CancellationToken ct)
    {
        var createParams = new CreateContainerParameters
        {
            Image = SandboxImage,
            Cmd = command,
            WorkingDir = SandboxWorkDir,
            User = "sandbox",
            NetworkDisabled = true,
            HostConfig = new HostConfig
            {
                ReadonlyRootfs = true,
                Memory = MemoryLimitBytes,
                MemorySwap = MemoryLimitBytes, // No swap
                NanoCPUs = NanoCpuLimit,
                Tmpfs = new Dictionary<string, string>
                {
                    ["/tmp"] = TmpfsSizeLimit,
                },
                SecurityOpt = ["no-new-privileges"],
                CapDrop = ["ALL"],
            },
        };

        var response = await _dockerClient.Containers.CreateContainerAsync(createParams, ct);
        return response.ID;
    }

    private async Task CopyCodeToContainerAsync(
        string containerId,
        string fileName,
        string code,
        CancellationToken ct)
    {
        using var tarStream = new MemoryStream();

        // Build a POSIX tar archive in memory
        await using (var tarWriter = new TarWriter(tarStream, leaveOpen: true))
        {
            var codeBytes = Encoding.UTF8.GetBytes(code);
            var entry = new PaxTarEntry(TarEntryType.RegularFile, fileName)
            {
                DataStream = new MemoryStream(codeBytes),
            };
            await tarWriter.WriteEntryAsync(entry, ct);
        }

        tarStream.Position = 0;

        await _dockerClient.Containers.ExtractArchiveToContainerAsync(
            containerId,
            new CopyToContainerParameters
            {
                Path = SandboxWorkDir,
            },
            tarStream,
            ct);
    }

    private async Task<(string Stdout, string Stderr)> GetContainerLogsAsync(
        string containerId,
        CancellationToken ct)
    {
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        var stdoutProgress = new Progress<string>(line => stdout.AppendLine(line));
        await _dockerClient.Containers.GetContainerLogsAsync(
            id: containerId,
            parameters: new ContainerLogsParameters
            {
                ShowStdout = true,
                ShowStderr = false,
                Follow = false,
            },
            progress: stdoutProgress,
            cancellationToken: ct);

        var stderrProgress = new Progress<string>(line => stderr.AppendLine(line));
        await _dockerClient.Containers.GetContainerLogsAsync(
            id: containerId,
            parameters: new ContainerLogsParameters
            {
                ShowStdout = false,
                ShowStderr = true,
                Follow = false,
            },
            progress: stderrProgress,
            cancellationToken: ct);

        return (stdout.ToString(), stderr.ToString());
    }

    private async Task RemoveContainerSafelyAsync(string containerId)
    {
        try
        {
            await _dockerClient.Containers.RemoveContainerAsync(
                containerId,
                new ContainerRemoveParameters
                {
                    Force = true,
                    RemoveVolumes = true,
                });

            _logger.LogDebug("Removed sandbox container {ContainerId}", containerId);
        }
        catch (Exception ex) // Intentional catch-all: container cleanup must suppress all errors to avoid leaking resources
        {
            _logger.LogWarning(ex, "Failed to remove sandbox container {ContainerId}", containerId);
        }
    }
}
