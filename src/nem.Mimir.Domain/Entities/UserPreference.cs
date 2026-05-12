namespace nem.Mimir.Domain.Entities;

using nem.Mimir.Domain.Common;
using nem.Mimir.Domain.ValueObjects;

public sealed class UserPreference : BaseAuditableEntity<UserPreferenceId>
{
    public Guid UserId { get; private set; }

    public Dictionary<string, Dictionary<string, object>> Settings { get; private set; } = new(StringComparer.OrdinalIgnoreCase);

    private UserPreference() { }

    public static UserPreference Create(Guid userId)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("User ID cannot be empty.", nameof(userId));

        return new UserPreference
        {
            Id = UserPreferenceId.New(),
            UserId = userId,
            Settings = CreateDefaultSettings(),
        };
    }

    public void UpdateSection(string section, Dictionary<string, object> values)
    {
        if (string.IsNullOrWhiteSpace(section))
            throw new ArgumentException("Section name cannot be empty.", nameof(section));

        if (values is null)
            throw new ArgumentNullException(nameof(values));

        var normalizedSection = NormalizeSection(section);

        if (!Settings.TryGetValue(normalizedSection, out var existingSection))
            throw new ArgumentException($"Unsupported settings section '{section}'.", nameof(section));

        foreach (var (key, value) in values)
        {
            existingSection[key] = value;
        }
    }

    public void ResetToDefaults()
    {
        Settings = CreateDefaultSettings();
    }

    private static string NormalizeSection(string section) => section.Trim().ToLowerInvariant();

    private static Dictionary<string, Dictionary<string, object>> CreateDefaultSettings() => new(StringComparer.OrdinalIgnoreCase)
    {
        ["general"] = new(StringComparer.OrdinalIgnoreCase)
        {
            ["language"] = "en",
            ["theme"] = "system",
            ["timezone"] = "UTC",
            ["defaultModelId"] = string.Empty,
        },
        ["chat"] = new(StringComparer.OrdinalIgnoreCase)
        {
            ["sendOnEnter"] = true,
            ["showTimestamps"] = true,
            ["renderLatex"] = true,
            ["codeHighlighting"] = true,
            ["autoScroll"] = true,
        },
        ["audio"] = new(StringComparer.OrdinalIgnoreCase)
        {
            ["speechToTextEngine"] = "default",
            ["textToSpeechEngine"] = "default",
            ["autoPlayback"] = false,
        },
        ["notifications"] = new(StringComparer.OrdinalIgnoreCase)
        {
            ["enableDesktop"] = true,
            ["enableSound"] = true,
            ["notifyOnMention"] = true,
        },
        ["appearance"] = new(StringComparer.OrdinalIgnoreCase)
        {
            ["fontSize"] = 14,
            ["colorScheme"] = "default",
            ["compactMode"] = false,
            ["sidebarCollapsed"] = false,
        },
        ["interface"] = new(StringComparer.OrdinalIgnoreCase)
        {
            ["showModelSelector"] = true,
            ["showSystemPrompt"] = true,
            ["showParameters"] = true,
        },
    };
}
