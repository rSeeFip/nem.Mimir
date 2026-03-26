using nem.Mimir.Application.Common.Models;
using nem.Mimir.Domain.Entities;
using ChannelId = nem.Contracts.Identity.ChannelId;
using ChannelMessageId = nem.Contracts.Identity.ChannelMessageId;
using EvaluationId = nem.Contracts.Identity.EvaluationId;
using FeedbackId = nem.Contracts.Identity.FeedbackId;
using FolderId = nem.Contracts.Identity.FolderId;
using ImageGenerationId = nem.Contracts.Identity.ImageGenerationId;
using LeaderboardEntryId = nem.Contracts.Identity.LeaderboardEntryId;
using NoteId = nem.Contracts.Identity.NoteId;
using PromptTemplateId = nem.Contracts.Identity.PromptTemplateId;
using UserPreferenceId = nem.Contracts.Identity.UserPreferenceId;

using nem.Mimir.Domain.Enums;
using nem.Mimir.Domain.ValueObjects;
using Riok.Mapperly.Abstractions;
using DomainFolderId = nem.Mimir.Domain.ValueObjects.FolderId;

namespace nem.Mimir.Application.Common.Mappings;

/// <summary>
/// Mapperly-based source-generated mapper for all entity-to-DTO mappings in the application layer.
/// </summary>
[Mapper]
public sealed partial class MimirMapper
{
    [MapperIgnoreSource(nameof(AuditEntry.DomainEvents))]
    public partial AuditEntryDto MapToAuditEntryDto(AuditEntry entity);

    [MapperIgnoreSource(nameof(SystemPrompt.CreatedBy))]
    [MapperIgnoreSource(nameof(SystemPrompt.UpdatedBy))]
    [MapperIgnoreSource(nameof(SystemPrompt.IsDeleted))]
    [MapperIgnoreSource(nameof(SystemPrompt.DeletedAt))]
    [MapperIgnoreSource(nameof(SystemPrompt.DomainEvents))]
    [MapperIgnoreSource(nameof(SystemPrompt.DomainEvents))]
    public partial SystemPromptDto MapToSystemPromptDto(SystemPrompt entity);

    [MapperIgnoreSource(nameof(PromptTemplate.CreatedBy))]
    [MapperIgnoreSource(nameof(PromptTemplate.UpdatedBy))]
    [MapperIgnoreSource(nameof(PromptTemplate.IsDeleted))]
    [MapperIgnoreSource(nameof(PromptTemplate.DeletedAt))]
    [MapperIgnoreSource(nameof(PromptTemplate.DomainEvents))]
    public partial PromptTemplateDto MapToPromptTemplateDto(PromptTemplate entity);

    private Guid MapSystemPromptId(SystemPromptId id) => id.Value;

    private Guid MapAuditEntryId(AuditEntryId id) => id.Value;

    private Guid MapUserId(UserId id) => id.Value;

    private Guid MapChannelId(ChannelId id) => id.Value;

    private Guid MapChannelMessageId(ChannelMessageId id) => id.Value;

    private Guid MapNoteId(NoteId id) => id.Value;

    private Guid MapEvaluationId(EvaluationId id) => id.Value;

    private Guid MapFeedbackId(FeedbackId id) => id.Value;

    private Guid MapFolderId(FolderId id) => id.Value;

    private Guid MapDomainFolderId(DomainFolderId id) => id.Value;

    private Guid MapImageGenerationId(ImageGenerationId id) => id.Value;

    private Guid MapPromptTemplateId(PromptTemplateId id) => id.Value;

    private Guid MapLeaderboardEntryId(LeaderboardEntryId id) => id.Value;

    private Guid MapUserPreferenceId(UserPreferenceId id) => id.Value;

    private Guid MapModelProfileId(ModelProfileId id) => id.Value;

    private Guid MapArenaConfigId(ArenaConfigId id) => id.Value;

    public UserDto MapToUserDto(User entity) =>
        new(
            entity.Id,
            entity.Username,
            entity.Email,
            entity.Role.ToString(),
            entity.IsActive,
            entity.LastLoginAt,
            entity.CreatedAt);

    public MessageDto MapToMessageDto(Message entity) =>
        new(
            entity.Id,
            entity.ConversationId,
            entity.Role.ToString(),
            entity.Content,
            entity.Model,
            entity.TokenCount,
            entity.CreatedAt);

    public ConversationDto MapToConversationDto(Conversation entity) =>
        new(
            entity.Id,
            entity.UserId,
            entity.FolderId,
            entity.IsPinned,
            entity.ShareId,
            entity.Tags.ToList(),
            entity.Title,
            entity.Status.ToString(),
            entity.Messages.Select(MapToMessageDto).ToList(),
            entity.CreatedAt,
            entity.UpdatedAt);

    public ConversationListDto MapToConversationListDto(Conversation entity) =>
        new(
            entity.Id,
            entity.UserId,
            entity.FolderId,
            entity.IsPinned,
            entity.ShareId,
            entity.Tags.ToList(),
            entity.Title,
            entity.Status.ToString(),
            entity.Messages.Count,
            entity.CreatedAt,
            entity.UpdatedAt);

    public FolderDto MapToFolderDto(Folder entity, int itemCount) =>
        new(
            entity.Id.Value,
            entity.UserId,
            entity.Name,
            entity.ParentId?.Value,
            entity.IsExpanded,
            itemCount,
            entity.CreatedAt,
            entity.UpdatedAt);

    public UserPreferenceDto MapToUserPreferenceDto(UserPreference entity)
    {
        var settings = entity.Settings.ToDictionary(
            section => section.Key,
            section => (IReadOnlyDictionary<string, object>)new Dictionary<string, object>(section.Value),
            StringComparer.OrdinalIgnoreCase);

        return new UserPreferenceDto(
            entity.Id.Value,
            entity.UserId,
            settings,
            entity.CreatedAt,
            entity.UpdatedAt);
    }

    public ModelProfileDto MapToModelProfileDto(ModelProfile entity) =>
        new(
            entity.Id.Value,
            entity.UserId,
            entity.Name,
            entity.ModelId,
            entity.Parameters.Temperature,
            entity.Parameters.TopP,
            entity.Parameters.MaxTokens,
            entity.Parameters.FrequencyPenalty,
            entity.Parameters.PresencePenalty,
            entity.Parameters.StopSequences.ToList(),
            entity.Parameters.SystemPromptOverride,
            entity.Parameters.ResponseFormat,
            entity.CreatedAt,
            entity.UpdatedAt);

    public ArenaConfigDto MapToArenaConfigDto(ArenaConfig entity) =>
        new(
            entity.Id.Value,
            entity.UserId,
            entity.ModelIds.ToList(),
            entity.IsBlindComparisonEnabled,
            entity.ShowModelNamesAfterVote,
            entity.CreatedAt,
            entity.UpdatedAt);
}
