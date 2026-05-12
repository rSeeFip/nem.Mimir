namespace nem.Mimir.Domain.Entities;

using nem.Mimir.Domain.Common;
using nem.Mimir.Domain.ValueObjects;

public sealed class ArenaConfig : BaseAuditableEntity<ArenaConfigId>
{
    public Guid UserId { get; private set; }
    public bool IsBlindComparisonEnabled { get; private set; }
    public bool ShowModelNamesAfterVote { get; private set; }

    private readonly List<string> _modelIds = [];
    public IReadOnlyCollection<string> ModelIds => _modelIds.AsReadOnly();

    private ArenaConfig() { }

    public static ArenaConfig Create(
        Guid userId,
        IReadOnlyCollection<string> modelIds,
        bool isBlindComparisonEnabled,
        bool showModelNamesAfterVote)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("User ID cannot be empty.", nameof(userId));

        var config = new ArenaConfig
        {
            Id = ArenaConfigId.New(),
            UserId = userId,
            IsBlindComparisonEnabled = isBlindComparisonEnabled,
            ShowModelNamesAfterVote = showModelNamesAfterVote,
        };

        config.ReplaceModelIds(modelIds);
        return config;
    }

    public void Update(
        IReadOnlyCollection<string> modelIds,
        bool isBlindComparisonEnabled,
        bool showModelNamesAfterVote)
    {
        ReplaceModelIds(modelIds);
        IsBlindComparisonEnabled = isBlindComparisonEnabled;
        ShowModelNamesAfterVote = showModelNamesAfterVote;
    }

    private void ReplaceModelIds(IReadOnlyCollection<string> modelIds)
    {
        if (modelIds is null)
            throw new ArgumentNullException(nameof(modelIds));

        var normalized = modelIds
            .Where(static id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalized.Count < 2)
            throw new ArgumentException("Arena configuration must contain at least two models.", nameof(modelIds));

        _modelIds.Clear();
        _modelIds.AddRange(normalized);
    }
}
