namespace nem.Mimir.Domain.Entities;

using System.Text.RegularExpressions;
using nem.Contracts.Identity;
using nem.Mimir.Domain.Common;
using nem.Mimir.Domain.ValueObjects;

public sealed class PromptTemplate : BaseAuditableEntity<PromptTemplateId>
{
    private static readonly Regex CommandPattern = new("^/[a-z0-9-]+$", RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));

    public Guid UserId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Command { get; private set; } = string.Empty;
    public string Content { get; private set; } = string.Empty;
    public bool IsShared { get; private set; }
    public int UsageCount { get; private set; }

    private readonly List<string> _tags = [];
    public IReadOnlyCollection<string> Tags => _tags.AsReadOnly();

    private readonly List<PromptTemplateVersionEntry> _versionHistory = [];
    public IReadOnlyCollection<PromptTemplateVersionEntry> VersionHistory => _versionHistory.AsReadOnly();

    private PromptTemplate() { }

    public static PromptTemplate Create(
        Guid userId,
        string title,
        string command,
        string content,
        IEnumerable<string>? tags = null,
        bool isShared = false)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("User ID cannot be empty.", nameof(userId));

        var promptTemplate = new PromptTemplate
        {
            Id = PromptTemplateId.New(),
            UserId = userId,
            Title = ValidateTitle(title),
            Command = ValidateCommand(command),
            Content = ValidateContent(content),
            IsShared = isShared,
            UsageCount = 0,
        };

        if (tags is not null)
        {
            foreach (var tag in tags)
            {
                promptTemplate.AddTag(tag);
            }
        }

        promptTemplate._versionHistory.Add(new PromptTemplateVersionEntry(promptTemplate.Content, DateTimeOffset.UtcNow));
        return promptTemplate;
    }

    public void Update(string title, string command, string content, IEnumerable<string>? tags = null)
    {
        var normalizedTitle = ValidateTitle(title);
        var normalizedCommand = ValidateCommand(command);
        var normalizedContent = ValidateContent(content);

        _versionHistory.Add(new PromptTemplateVersionEntry(normalizedContent, DateTimeOffset.UtcNow));

        Title = normalizedTitle;
        Command = normalizedCommand;
        Content = normalizedContent;

        _tags.Clear();
        if (tags is not null)
        {
            foreach (var tag in tags)
            {
                AddTag(tag);
            }
        }
    }

    public void SetShared(bool isShared)
    {
        IsShared = isShared;
    }

    public void IncrementUsage()
    {
        UsageCount++;
    }

    public void AddTag(string tag)
    {
        var normalizedTag = ConversationTag.Create(tag).Value;
        if (_tags.Any(existing => string.Equals(existing, normalizedTag, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        _tags.Add(normalizedTag);
    }

    public void RemoveTag(string tag)
    {
        var normalizedTag = ConversationTag.Create(tag).Value;
        var existingTag = _tags.FirstOrDefault(existing => string.Equals(existing, normalizedTag, StringComparison.OrdinalIgnoreCase));
        if (existingTag is null)
        {
            return;
        }

        _tags.Remove(existingTag);
    }

    private static string ValidateTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title cannot be empty.", nameof(title));

        return title.Trim();
    }

    private static string ValidateCommand(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
            throw new ArgumentException("Command cannot be empty.", nameof(command));

        var normalizedCommand = command.Trim().ToLowerInvariant();
        if (!CommandPattern.IsMatch(normalizedCommand))
            throw new ArgumentException("Command must match format '/command-name'.", nameof(command));

        return normalizedCommand;
    }

    private static string ValidateContent(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Content cannot be empty.", nameof(content));

        return content.Trim();
    }
}

public sealed record PromptTemplateVersionEntry(string Content, DateTimeOffset Timestamp);
