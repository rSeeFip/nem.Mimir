namespace Mimir.Api.Hubs;

using System.Diagnostics;

public static class WebWidgetActivitySource
{
    public const string Name = "Mimir.WebWidget";

    public static readonly ActivitySource Instance = new(Name);
}
