namespace nem.Mimir.Teams.Tests.Services;

using System.Security.Claims;
using nem.Mimir.Teams.Services;
using Shouldly;

public sealed class AadClaimMapperTests
{
    private readonly AadClaimMapper _sut = new();

    [Fact]
    public void Map_WithObjectIdClaim_ReturnsUserId()
    {
        var claims = new ClaimsIdentity(
        [
            new Claim("http://schemas.microsoft.com/identity/claims/objectidentifier", "user-oid-123"),
            new Claim("name", "Jane Doe"),
        ]);

        var (userId, displayName) = _sut.Map(claims);

        userId.ShouldBe("user-oid-123");
        displayName.ShouldBe("Jane Doe");
    }

    [Fact]
    public void Map_WithNameIdentifierFallback_ReturnsUserId()
    {
        var claims = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "name-id-456"),
            new Claim("name", "John Smith"),
        ]);

        var (userId, displayName) = _sut.Map(claims);

        userId.ShouldBe("name-id-456");
        displayName.ShouldBe("John Smith");
    }

    [Fact]
    public void Map_NoIdentityClaim_ReturnsUnknown()
    {
        var claims = new ClaimsIdentity([new Claim("name", "Ghost")]);

        var (userId, displayName) = _sut.Map(claims);

        userId.ShouldBe("unknown");
        displayName.ShouldBe("Ghost");
    }

    [Fact]
    public void Map_NoNameClaim_ReturnsNullDisplayName()
    {
        var claims = new ClaimsIdentity(
        [
            new Claim("http://schemas.microsoft.com/identity/claims/objectidentifier", "oid-789"),
        ]);

        var (_, displayName) = _sut.Map(claims);

        displayName.ShouldBeNull();
    }

    [Fact]
    public void Map_EmptyClaims_ReturnsUnknownAndNull()
    {
        var claims = new ClaimsIdentity();

        var (userId, displayName) = _sut.Map(claims);

        userId.ShouldBe("unknown");
        displayName.ShouldBeNull();
    }
}
