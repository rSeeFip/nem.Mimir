using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mimir.Telegram.Configuration;
using Mimir.Telegram.Services;
using NSubstitute;
using Shouldly;

namespace Mimir.Telegram.Tests.Services;

public sealed class AuthenticationServiceTests
{
    private readonly IHttpClientFactory _httpClientFactory = Substitute.For<IHttpClientFactory>();
    private readonly IOptions<TelegramSettings> _settings;
    private readonly ILogger<AuthenticationService> _logger = Substitute.For<ILogger<AuthenticationService>>();
    private readonly AuthenticationService _sut;

    public AuthenticationServiceTests()
    {
        _settings = Options.Create(new TelegramSettings
        {
            AuthCodeExpiryMinutes = 5,
            KeycloakUrl = "http://localhost:8080/realms/mimir",
            KeycloakClientId = "mimir-api",
            KeycloakClientSecret = "secret"
        });
        _sut = new AuthenticationService(_httpClientFactory, _settings, _logger);
    }

    [Fact]
    public void GenerateAuthCode_ReturnsNonEmptyString()
    {
        var code = _sut.GenerateAuthCode(12345);

        code.ShouldNotBeNullOrWhiteSpace();
        code.Length.ShouldBe(8);
    }

    [Fact]
    public void GenerateAuthCode_ReturnsUppercaseCode()
    {
        var code = _sut.GenerateAuthCode(12345);

        code.ShouldBe(code.ToUpperInvariant());
    }

    [Fact]
    public void GenerateAuthCode_ReturnsDifferentCodesForSameUser()
    {
        var code1 = _sut.GenerateAuthCode(12345);
        var code2 = _sut.GenerateAuthCode(12345);

        code1.ShouldNotBe(code2);
    }

    [Fact]
    public void ValidateAuthCode_ReturnsUserId_ForValidCode()
    {
        var code = _sut.GenerateAuthCode(12345);

        var userId = _sut.ValidateAuthCode(code);

        userId.ShouldBe(12345);
    }

    [Fact]
    public void ValidateAuthCode_ReturnsNull_ForInvalidCode()
    {
        var userId = _sut.ValidateAuthCode("INVALID1");

        userId.ShouldBeNull();
    }

    [Fact]
    public void ValidateAuthCode_ReturnsNull_OnSecondUse_OneTimeOnly()
    {
        var code = _sut.GenerateAuthCode(12345);

        _sut.ValidateAuthCode(code).ShouldBe(12345);
        _sut.ValidateAuthCode(code).ShouldBeNull();
    }

    [Fact]
    public void ValidateAuthCode_IsCaseInsensitive()
    {
        var code = _sut.GenerateAuthCode(12345);

        var userId = _sut.ValidateAuthCode(code.ToLowerInvariant());

        userId.ShouldBe(12345);
    }

    [Fact]
    public async Task AuthenticateWithKeycloakAsync_ReturnsNull_WhenKeycloakUrlIsEmpty()
    {
        var settings = Options.Create(new TelegramSettings { KeycloakUrl = "" });
        var sut = new AuthenticationService(_httpClientFactory, settings, _logger);

        var result = await sut.AuthenticateWithKeycloakAsync("user", "pass", CancellationToken.None);

        result.ShouldBeNull();
    }
}
