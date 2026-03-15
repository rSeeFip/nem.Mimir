using Microsoft.Extensions.DependencyInjection;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Domain.Plugins;

namespace nem.Mimir.Infrastructure.Plugins.BuiltIn;

/// <summary>
/// Built-in plugin that executes code in an isolated Docker sandbox.
/// Delegates to <see cref="ISandboxService"/> resolved per-execution via a scope factory.
/// </summary>
internal sealed class CodeRunnerPlugin : IPlugin
{
    private static readonly HashSet<string> SupportedLanguages = new(StringComparer.OrdinalIgnoreCase) { "python", "javascript" };

    private readonly IServiceScopeFactory _scopeFactory;

    public CodeRunnerPlugin(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public string Id => "mimir.builtin.code-runner";
    public string Name => "Code Runner";
    public string Version => "1.0.0";
    public string Description => "Executes Python or JavaScript code in an isolated Docker sandbox.";

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

        using var scope = _scopeFactory.CreateScope();
        var sandbox = scope.ServiceProvider.GetRequiredService<ISandboxService>();
        var result = await sandbox.ExecuteAsync(code, language, ct);

        if (result.TimedOut)
        {
            return PluginResult.Failure("Code execution timed out.");
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
