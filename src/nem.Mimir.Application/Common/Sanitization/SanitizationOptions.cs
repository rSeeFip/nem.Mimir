using Microsoft.Extensions.Options;

namespace nem.Mimir.Application.Common.Sanitization;

public sealed class SanitizationOptions
{
    public const string SectionName = "Sanitization";

    public SanitizationMode DefaultMode { get; init; } = SanitizationMode.Sanitize;

    public Dictionary<string, SanitizationMode> ChannelOverrides { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public SanitizationMode GetModeForChannel(string channel)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(channel);

        return ChannelOverrides.TryGetValue(channel, out var mode) ? mode : DefaultMode;
    }
}
