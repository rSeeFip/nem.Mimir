namespace nem.Mimir.Application.Common.Models;

public sealed record ConversationAttachmentDto(
    Guid DocumentId,
    string FileName,
    Guid KnowledgeCollectionId,
    string ProcessingStatus);

public sealed record ConversationContextDto(
    Guid ConversationId,
    string Query,
    IReadOnlyList<KnowledgeSearchResultDto> KnowledgeSources);
