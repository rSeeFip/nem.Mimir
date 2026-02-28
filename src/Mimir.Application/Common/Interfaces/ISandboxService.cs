using Mimir.Application.Common.Models;

namespace Mimir.Application.Common.Interfaces;

/// <summary>
/// Service interface for executing code in an isolated Docker sandbox container.
/// Provides secure, resource-limited code execution for Python and JavaScript.
/// </summary>
public interface ISandboxService
{
    /// <summary>
    /// Executes the given code in an isolated Docker container and returns the result.
    /// </summary>
    /// <param name="code">The source code to execute.</param>
    /// <param name="language">The programming language ("python" or "javascript").</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The execution result containing stdout, stderr, exit code, and timing information.</returns>
    Task<CodeExecutionResult> ExecuteAsync(string code, string language, CancellationToken ct = default);
}
