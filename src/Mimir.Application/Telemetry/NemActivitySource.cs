using System.Diagnostics;

namespace Mimir.Application.Telemetry;

/// <summary>
/// Central activity source for nem.Mimir distributed tracing.
/// Provides a static ActivitySource for instrumenting operations across the application.
/// </summary>
public static class NemActivitySource
{
    /// <summary>
    /// The static ActivitySource for nem.Mimir tracing.
    /// Name: "nem.Mimir", Version: "1.0.0"
    /// </summary>
    public static readonly ActivitySource Source = new("nem.Mimir", "1.0.0");

    /// <summary>
    /// Starts a new activity with the given operation name.
    /// </summary>
    /// <param name="operationName">The name of the operation being traced.</param>
    /// <param name="kind">The kind of activity (default: Internal).</param>
    /// <returns>A new Activity if tracing is enabled; otherwise null.</returns>
    public static Activity? StartActivity(string operationName, ActivityKind kind = ActivityKind.Internal)
    {
        return Source.StartActivity(operationName, kind);
    }

    /// <summary>
    /// Starts a new server-side activity (typically for request handling).
    /// </summary>
    /// <param name="operationName">The name of the operation being traced.</param>
    /// <returns>A new Activity if tracing is enabled; otherwise null.</returns>
    public static Activity? StartServerActivity(string operationName)
    {
        return Source.StartActivity(operationName, ActivityKind.Server);
    }

    /// <summary>
    /// Starts a new internal activity for cross-service communication.
    /// </summary>
    /// <param name="operationName">The name of the operation being traced.</param>
    /// <returns>A new Activity if tracing is enabled; otherwise null.</returns>
    public static Activity? StartInternalActivity(string operationName)
    {
        return Source.StartActivity(operationName, ActivityKind.Internal);
    }
}
