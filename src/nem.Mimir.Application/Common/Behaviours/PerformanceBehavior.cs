using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace nem.Mimir.Application.Common.Behaviours;

/// <summary>
/// Wolverine middleware that warns when request handling exceeds a performance threshold.
/// Logs a warning for requests that take longer than 500 milliseconds.
/// Applied globally via <c>opts.Policies.AddMiddleware&lt;PerformanceMiddleware&gt;()</c>.
/// </summary>
/// <remarks>
/// Uses the static middleware pattern: <see cref="Before"/> returns a <see cref="Stopwatch"/>
/// that Wolverine passes into <see cref="Finally"/> automatically.
/// </remarks>
public static class PerformanceMiddleware
{
    private const int WarningThresholdMilliseconds = 500;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Stopwatch Before()
    {
        var stopwatch = new Stopwatch();
        stopwatch.Start();
        return stopwatch;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Finally(Stopwatch stopwatch, ILogger logger, Envelope envelope)
    {
        stopwatch.Stop();

        var elapsedMilliseconds = stopwatch.ElapsedMilliseconds;

        if (elapsedMilliseconds > WarningThresholdMilliseconds)
        {
            logger.LogWarning(
                "Long running request: {RequestName} ({ElapsedMilliseconds}ms)",
                envelope.MessageType,
                elapsedMilliseconds);
        }
    }
}
