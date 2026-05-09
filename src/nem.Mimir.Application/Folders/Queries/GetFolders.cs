using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Mappings;
using nem.Mimir.Application.Common.Models;

namespace nem.Mimir.Application.Folders.Queries;

public sealed record GetFoldersQuery() : IQuery<IReadOnlyList<FolderDto>>;

internal sealed class GetFoldersQueryHandler
{
    private readonly IFolderRepository _folderRepository;
    private readonly IConversationRepository _conversationRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly MimirMapper _mapper;

    public GetFoldersQueryHandler(
        IFolderRepository folderRepository,
        IConversationRepository conversationRepository,
        ICurrentUserService currentUserService,
        MimirMapper mapper)
    {
        _folderRepository = folderRepository;
        _conversationRepository = conversationRepository;
        _currentUserService = currentUserService;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<FolderDto>> Handle(GetFoldersQuery request, CancellationToken cancellationToken)
    {
        var userId = ResolveCurrentUserId(_currentUserService);

        var folders = await _folderRepository.GetByUserIdAsync(userId, cancellationToken);
        var conversations = await _conversationRepository.GetAllByUserIdAsync(userId, cancellationToken);

        var itemCountByFolderId = conversations
            .Where(c => c.FolderId.HasValue)
            .GroupBy(c => c.FolderId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        return folders
            .Select(folder => _mapper.MapToFolderDto(folder, itemCountByFolderId.GetValueOrDefault(folder.Id.Value, 0)))
            .ToList();
    }

    private static Guid ResolveCurrentUserId(ICurrentUserService currentUserService)
    {
        var userId = currentUserService.UserId
            ?? throw new ForbiddenAccessException("User is not authenticated.");

        if (!Guid.TryParse(userId, out var parsedUserId))
            throw new ForbiddenAccessException("Current user identifier is invalid.");

        return parsedUserId;
    }
}
