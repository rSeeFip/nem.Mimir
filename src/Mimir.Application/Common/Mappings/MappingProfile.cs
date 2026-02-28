using AutoMapper;
using Mimir.Application.Common.Models;
using Mimir.Domain.Entities;

namespace Mimir.Application.Common.Mappings;

/// <summary>
/// AutoMapper profile that configures all entity-to-DTO mappings for the application layer.
/// </summary>
public sealed class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<User, UserDto>()
            .ForCtorParam(nameof(UserDto.Role), opt => opt.MapFrom(src => src.Role.ToString()));

        CreateMap<Conversation, ConversationDto>()
            .ForCtorParam(nameof(ConversationDto.Status), opt => opt.MapFrom(src => src.Status.ToString()))
            .ForCtorParam(nameof(ConversationDto.Messages), opt => opt.MapFrom(src => src.Messages));

        CreateMap<Conversation, ConversationListDto>()
            .ForCtorParam(nameof(ConversationListDto.Status), opt => opt.MapFrom(src => src.Status.ToString()))
            .ForCtorParam(nameof(ConversationListDto.MessageCount), opt => opt.MapFrom(src => src.Messages.Count));

        CreateMap<Message, MessageDto>()
            .ForCtorParam(nameof(MessageDto.Role), opt => opt.MapFrom(src => src.Role.ToString()));

        CreateMap<AuditEntry, AuditEntryDto>();
    }
}
