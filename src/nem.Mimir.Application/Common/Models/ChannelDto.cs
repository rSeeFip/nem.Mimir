using FluentValidation;

namespace nem.Mimir.Application.Common.Models;

public sealed record ChannelDto(
    Guid Id,
    Guid CreatedByUserId,
    string Name,
    string? Description,
    string AccessControl,
    int MemberCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record ChannelListDto(
    Guid Id,
    string Name,
    string? Description,
    string AccessControl,
    int MemberCount,
    DateTimeOffset CreatedAt);

public sealed record ChannelMemberDto(
    Guid UserId,
    string Role,
    DateTimeOffset JoinedAt);

public sealed record ChannelMessageDto(
    Guid Id,
    Guid ChannelId,
    Guid UserId,
    string Content,
    Guid? ParentId,
    bool IsPinned,
    IReadOnlyList<MessageReactionDto> Reactions,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record MessageReactionDto(
    string Emoji,
    int Count,
    bool HasReacted);

public sealed class ChannelDtoValidator : AbstractValidator<ChannelDto>
{
    public ChannelDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Channel name is required.")
            .MaximumLength(200).WithMessage("Channel name must not exceed 200 characters.");

        RuleFor(x => x.AccessControl)
            .NotEmpty().WithMessage("Access control is required.");
    }
}

public sealed class ChannelMessageDtoValidator : AbstractValidator<ChannelMessageDto>
{
    public ChannelMessageDtoValidator()
    {
        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("Message content is required.");
    }
}

public sealed class MessageReactionDtoValidator : AbstractValidator<MessageReactionDto>
{
    public MessageReactionDtoValidator()
    {
        RuleFor(x => x.Emoji)
            .NotEmpty().WithMessage("Reaction emoji is required.");

        RuleFor(x => x.Count)
            .GreaterThanOrEqualTo(0).WithMessage("Reaction count cannot be negative.");
    }
}
