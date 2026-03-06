namespace Mimir.Signal.Services;

using System.Diagnostics;

/// <summary>
/// OpenTelemetry ActivitySource for the Mimir.Signal service.
/// </summary>
internal static class SignalActivitySource
{
    public const string Name = "Mimir.Signal";

    public static readonly ActivitySource Instance = new(Name);
}
