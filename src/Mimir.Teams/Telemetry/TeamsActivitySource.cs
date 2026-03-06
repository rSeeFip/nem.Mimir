using System.Diagnostics;

namespace Mimir.Teams.Telemetry;

internal static class TeamsActivitySource
{
    public static readonly ActivitySource Source = new("Mimir.Teams", "1.0.0");

    public static Activity? StartServerActivity(string operationName)
        => Source.StartActivity(operationName, ActivityKind.Server);

    public static Activity? StartClientActivity(string operationName)
        => Source.StartActivity(operationName, ActivityKind.Client);
}
