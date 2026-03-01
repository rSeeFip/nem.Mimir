using Mimir.Domain.Common;

namespace Mimir.Domain.Plugins;

/// <summary>
/// Execution context passed to a plugin when it runs.
/// </summary>
public sealed record PluginContext
{
    public string UserId { get; }
    public IReadOnlyDictionary<string, object> Parameters { get; }

    private PluginContext(string userId, IReadOnlyDictionary<string, object> parameters)
    {
        UserId = userId;
        Parameters = parameters;
    }

    public static PluginContext Create(string userId, IReadOnlyDictionary<string, object>? parameters)
    {
        Guard.Against.NullOrEmpty(userId, nameof(userId));
        return new PluginContext(userId, parameters ?? new Dictionary<string, object>());
    }
}
