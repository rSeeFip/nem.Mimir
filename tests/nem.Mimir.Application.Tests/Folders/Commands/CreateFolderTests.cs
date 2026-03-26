using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Mappings;
using nem.Mimir.Application.Folders.Commands;
using nem.Mimir.Domain.Entities;
using NSubstitute;
using Shouldly;

namespace nem.Mimir.Application.Tests.Folders.Commands;

public sealed class CreateFolderTests
{
    private readonly IFolderRepository _folderRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly MimirMapper _mapper;
    private readonly CreateFolderCommandHandler _handler;

    public CreateFolderTests()
    {
        _folderRepository = Substitute.For<IFolderRepository>();
        _currentUserService = Substitute.For<ICurrentUserService>();
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _mapper = new MimirMapper();

        _handler = new CreateFolderCommandHandler(_folderRepository, _currentUserService, _unitOfWork, _mapper);
    }

    [Fact]
    public async Task Handle_ValidRequest_ShouldCreateFolder()
    {
        var userId = Guid.NewGuid();
        _currentUserService.UserId.Returns(userId.ToString());
        _folderRepository.CreateAsync(Arg.Any<Folder>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<Folder>());

        var result = await _handler.Handle(new CreateFolderCommand("Work", null), CancellationToken.None);

        result.Name.ShouldBe("Work");
        result.UserId.ShouldBe(userId);
        result.ItemCount.ShouldBe(0);
        await _folderRepository.Received(1).CreateAsync(Arg.Any<Folder>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Unauthenticated_ShouldThrowForbidden()
    {
        _currentUserService.UserId.Returns((string?)null);

        await Should.ThrowAsync<ForbiddenAccessException>(() =>
            _handler.Handle(new CreateFolderCommand("Work", null), CancellationToken.None));
    }
}
