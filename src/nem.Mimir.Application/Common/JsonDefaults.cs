using System.Text.Json;
using System.Text.Json.Serialization;

namespace nem.Mimir.Application.Common;

/// <summary>
/// Shared JSON serialization options for consistent serialization across the application.
/// </summary>
public static class JsonDefaults
{
    /// <summary>
    /// Default <see cref="JsonSerializerOptions"/> with camelCase naming, null-value suppression,
    /// and string-based enum serialization.
    /// </summary>
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };
}
