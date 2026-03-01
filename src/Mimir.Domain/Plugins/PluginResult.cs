using Mimir.Domain.Common;

namespace Mimir.Domain.Plugins;

/// <summary>
/// Represents the outcome of a plugin execution.
/// </summary>
public sealed record PluginResult
{
    public bool IsSuccess { get; }
    public string? ErrorMessage { get; }
    public IReadOnlyDictionary<string, object> Data { get; }

    private PluginResult(bool isSuccess, string? errorMessage, IReadOnlyDictionary<string, object> data)
    {
        IsSuccess = isSuccess;
        ErrorMessage = errorMessage;
        Data = data;
    }

    public static PluginResult Success(IReadOnlyDictionary<string, object>? data = null) =>
        new(true, null, data ?? new Dictionary<string, object>());

    public static PluginResult Failure(string errorMessage)
    {
        Guard.Against.NullOrEmpty(errorMessage, nameof(errorMessage));
        return new(false, errorMessage, new Dictionary<string, object>());
    }
}
