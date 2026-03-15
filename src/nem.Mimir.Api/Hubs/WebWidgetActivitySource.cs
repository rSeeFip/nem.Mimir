namespace nem.Mimir.Api.Hubs;

using System.Diagnostics;

public static class WebWidgetActivitySource
{
    public const string Name = "nem.Mimir.WebWidget";

    public static readonly ActivitySource Instance = new(Name);
}
