using nem.Contracts.Sandbox;
using nem.Mimir.Domain.Plugins;

namespace nem.Mimir.Infrastructure.Plugins.BuiltIn;

/// <summary>
/// Built-in plugin that executes code in an isolated sandbox session via OpenSandbox.
/// Delegates to <see cref="ISandboxProvider"/> to create a per-execution session.
/// </summary>
internal sealed class CodeRunnerPlugin : IBuiltInPlugin
{
    private static readonly HashSet<string> SupportedLanguages = new(StringComparer.OrdinalIgnoreCase) { "python", "javascript" };

    private readonly ISandboxProvider _sandboxProvider;

    public CodeRunnerPlugin(ISandboxProvider sandboxProvider)
    {
        _sandboxProvider = sandboxProvider;
    }

    public string Id => "mimir.builtin.code-runner";
    public string Name => "Code Runner";
    public string Version => "1.0.0";
    public string Description => "Executes Python or JavaScript code in an isolated sandbox session.";

    public async Task<PluginResult> ExecuteAsync(PluginContext context, CancellationToken ct = default)
    {
        if (!context.Parameters.TryGetValue("code", out var codeObj) || codeObj is not string code || string.IsNullOrWhiteSpace(code))
        {
            return PluginResult.Failure("Required parameter 'code' is missing or empty.");
        }

        var language = "python";
        if (context.Parameters.TryGetValue("language", out var langObj) && langObj is string lang && !string.IsNullOrWhiteSpace(lang))
        {
            language = lang;
        }

        if (!SupportedLanguages.Contains(language))
        {
            return PluginResult.Failure($"Unsupported language '{language}'. Supported languages: python, javascript.");
        }

        var config = new SandboxConfig(Image: "sandbox:latest");
        var session = await _sandboxProvider.CreateSessionAsync(config, SandboxPolicyClass.CreationLocked, ct);
        SandboxExecutionResult result;
        try
        {
            result = await session.ExecuteAsync(code, language, cancellationToken: ct);
        }
        finally
        {
            await _sandboxProvider.DestroySessionAsync(session.SessionId, ct);
        }

        if (result.ExitCode != 0)
        {
            var errorDetail = string.IsNullOrWhiteSpace(result.Stderr) ? "Unknown error" : result.Stderr;
            return PluginResult.Failure($"Code execution failed (exit code {result.ExitCode}): {errorDetail}");
        }

        return PluginResult.Success(new Dictionary<string, object>
        {
            ["stdout"] = result.Stdout,
            ["stderr"] = result.Stderr,
            ["exitCode"] = result.ExitCode,
            ["executionTimeMs"] = result.ExecutionTimeMs,
        });
    }

    public Task InitializeAsync(CancellationToken ct = default) => Task.CompletedTask;

    public Task ShutdownAsync(CancellationToken ct = default) => Task.CompletedTask;
}
