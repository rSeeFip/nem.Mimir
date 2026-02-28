namespace Mimir.Application.Common.Models;

/// <summary>
/// Represents the result of executing code in a sandboxed Docker container.
/// </summary>
/// <param name="Stdout">The captured standard output from the execution.</param>
/// <param name="Stderr">The captured standard error from the execution.</param>
/// <param name="ExitCode">The process exit code (0 typically indicates success).</param>
/// <param name="ExecutionTimeMs">The total wall-clock execution time in milliseconds.</param>
/// <param name="TimedOut">Whether the execution was terminated due to exceeding the timeout limit.</param>
public sealed record CodeExecutionResult(
    string Stdout,
    string Stderr,
    int ExitCode,
    long ExecutionTimeMs,
    bool TimedOut);
