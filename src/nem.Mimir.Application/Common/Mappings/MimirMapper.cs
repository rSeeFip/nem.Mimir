using nem.Mimir.Application.Common.Models;
using nem.Mimir.Domain.Entities;
using nem.Mimir.Domain.Enums;
using Riok.Mapperly.Abstractions;

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
    public partial SystemPromptDto MapToSystemPromptDto(SystemPrompt entity);

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
            entity.Title,
            entity.Status.ToString(),
            entity.Messages.Select(MapToMessageDto).ToList(),
            entity.CreatedAt,
            entity.UpdatedAt);

    public ConversationListDto MapToConversationListDto(Conversation entity) =>
        new(
            entity.Id,
            entity.UserId,
            entity.Title,
            entity.Status.ToString(),
            entity.Messages.Count,
            entity.CreatedAt,
            entity.UpdatedAt);
}
