using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Mappings;
using nem.Mimir.Application.Knowledge.Commands;
using nem.Mimir.Domain.Entities;
using NSubstitute;
using Shouldly;

namespace nem.Mimir.Application.Tests.Knowledge;

public sealed class CreateKnowledgeCollectionTests
{
    private readonly IKnowledgeCollectionRepository _repository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly MimirMapper _mapper;
    private readonly CreateKnowledgeCollectionCommandHandler _handler;

    public CreateKnowledgeCollectionTests()
    {
        _repository = Substitute.For<IKnowledgeCollectionRepository>();
        _currentUserService = Substitute.For<ICurrentUserService>();
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _mapper = new MimirMapper();

        _handler = new CreateKnowledgeCollectionCommandHandler(_repository, _currentUserService, _unitOfWork, _mapper);
    }

    [Fact]
    public async Task Handle_ValidRequest_ShouldCreateCollection()
    {
        var userId = Guid.NewGuid();
        _currentUserService.UserId.Returns(userId.ToString());
        _repository.CreateAsync(Arg.Any<KnowledgeCollection>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<KnowledgeCollection>());

        var result = await _handler.Handle(new CreateKnowledgeCollectionCommand("Research", "RAG source"), CancellationToken.None);

        result.UserId.ShouldBe(userId);
        result.Name.ShouldBe("Research");
        result.Description.ShouldBe("RAG source");
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Validator_EmptyName_ShouldFail()
    {
        var validator = new CreateKnowledgeCollectionCommandValidator();
        var validation = validator.Validate(new CreateKnowledgeCollectionCommand("", "desc"));

        validation.IsValid.ShouldBeFalse();
        validation.Errors.ShouldContain(x => x.PropertyName == "Name");
    }
}
