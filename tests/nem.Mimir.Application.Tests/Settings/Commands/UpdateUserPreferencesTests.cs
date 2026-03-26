using nem.Mimir.Application.Common.Exceptions;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Mappings;
using nem.Mimir.Application.Settings.Commands;
using nem.Mimir.Application.Settings.Queries;
using nem.Mimir.Domain.Entities;
using NSubstitute;
using Shouldly;

namespace nem.Mimir.Application.Tests.Settings.Commands;

public sealed class UpdateUserPreferencesTests
{
    private readonly IUserPreferenceRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly MimirMapper _mapper;

    public UpdateUserPreferencesTests()
    {
        _repository = Substitute.For<IUserPreferenceRepository>();
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _currentUserService = Substitute.For<ICurrentUserService>();
        _mapper = new MimirMapper();
    }

    [Fact]
    public async Task GetUserPreferences_WhenMissing_ShouldCreateDefaults()
    {
        var userId = Guid.NewGuid();
        _currentUserService.UserId.Returns(userId.ToString());
        _repository.GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns((UserPreference?)null);
        _repository.CreateAsync(Arg.Any<UserPreference>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<UserPreference>());

        var handler = new GetUserPreferencesQueryHandler(_repository, _unitOfWork, _mapper, _currentUserService);

        var result = await handler.Handle(new GetUserPreferencesQuery(), CancellationToken.None);

        result.UserId.ShouldBe(userId);
        result.Settings.Keys.ShouldContain("general");
        await _repository.Received(1).CreateAsync(Arg.Any<UserPreference>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateUserPreferences_ShouldPersistSectionUpdate()
    {
        var userId = Guid.NewGuid();
        var preference = UserPreference.Create(userId);

        _currentUserService.UserId.Returns(userId.ToString());
        _repository.GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(preference);

        var handler = new UpdateUserPreferencesCommandHandler(_repository, _unitOfWork, _mapper, _currentUserService);

        var result = await handler.Handle(
            new UpdateUserPreferencesCommand(
                Section: "general",
                Values: new Dictionary<string, object> { ["language"] = "es" },
                Settings: null),
            CancellationToken.None);

        result.Settings["general"]["language"].ShouldBe("es");
        await _repository.Received(1).UpdateAsync(preference, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResetUserPreferences_ShouldRestoreDefaults()
    {
        var userId = Guid.NewGuid();
        var preference = UserPreference.Create(userId);
        preference.UpdateSection("general", new Dictionary<string, object> { ["language"] = "it" });

        _currentUserService.UserId.Returns(userId.ToString());
        _repository.GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(preference);

        var handler = new ResetUserPreferencesCommandHandler(_repository, _unitOfWork, _mapper, _currentUserService);

        var result = await handler.Handle(new ResetUserPreferencesCommand(), CancellationToken.None);

        result.Settings["general"]["language"].ShouldBe("en");
        await _repository.Received(1).UpdateAsync(preference, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateUserPreferences_WhenUnauthenticated_ShouldThrowForbidden()
    {
        _currentUserService.UserId.Returns((string?)null);
        var handler = new UpdateUserPreferencesCommandHandler(_repository, _unitOfWork, _mapper, _currentUserService);

        await Should.ThrowAsync<ForbiddenAccessException>(() =>
            handler.Handle(
                new UpdateUserPreferencesCommand("general", new Dictionary<string, object> { ["theme"] = "dark" }, null),
                CancellationToken.None));
    }
}
