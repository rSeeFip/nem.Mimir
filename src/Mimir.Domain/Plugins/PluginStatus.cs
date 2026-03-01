namespace Mimir.Domain.Plugins;

/// <summary>
/// Status of a loaded plugin.
/// </summary>
public enum PluginStatus
{
    Unloaded = 0,
    Loaded = 1,
    Running = 2,
    Error = 3
}
