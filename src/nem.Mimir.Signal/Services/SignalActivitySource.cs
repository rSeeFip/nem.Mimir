namespace nem.Mimir.Signal.Services;

using System.Diagnostics;

/// <summary>
/// OpenTelemetry ActivitySource for the nem.Mimir.Signal service.
/// </summary>
internal static class SignalActivitySource
{
    public const string Name = "nem.Mimir.Signal";

    public static readonly ActivitySource Instance = new(Name);
}
