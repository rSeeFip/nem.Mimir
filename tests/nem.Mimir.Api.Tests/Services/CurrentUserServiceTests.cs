using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using nem.Mimir.Api.Services;
using NSubstitute;
using Shouldly;

namespace nem.Mimir.Api.Tests.Services;

public sealed class CurrentUserServiceTests
{
    private static CurrentUserService CreateService(ClaimsPrincipal? user = null)
    {
        var httpContext = new DefaultHttpContext();
        if (user is not null)
        {
            httpContext.User = user;
        }

        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(httpContext);
        return new CurrentUserService(accessor);
    }

    private static CurrentUserService CreateServiceWithNoHttpContext()
    {
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns((HttpContext?)null);
        return new CurrentUserService(accessor);
    }

    // ── No authenticated user → returns null ────────────────────────────────

    [Fact]
    public void UserId_NoAuthenticatedUser_ReturnsNull()
    {
        // Arrange — default identity is not authenticated
        var service = CreateService();

        // Act & Assert
        service.UserId.ShouldBeNull();
    }

    [Fact]
    public void IsAuthenticated_NoAuthenticatedUser_ReturnsFalse()
    {
        var service = CreateService();
        service.IsAuthenticated.ShouldBeFalse();
    }

    [Fact]
    public void UserId_NullHttpContext_ReturnsNull()
    {
        var service = CreateServiceWithNoHttpContext();
        service.UserId.ShouldBeNull();
    }

    [Fact]
    public void IsAuthenticated_NullHttpContext_ReturnsFalse()
    {
        var service = CreateServiceWithNoHttpContext();
        service.IsAuthenticated.ShouldBeFalse();
    }

    // ── Missing "sub" (NameIdentifier) claim → returns null ─────────────────

    [Fact]
    public void UserId_AuthenticatedButNoSubClaim_ReturnsNull()
    {
        // Arrange — authenticated user with no NameIdentifier claim
        var identity = new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.Name, "testuser") },
            "TestAuth");
        var user = new ClaimsPrincipal(identity);

        var service = CreateService(user);

        // Act & Assert
        service.UserId.ShouldBeNull();
        service.IsAuthenticated.ShouldBeTrue();
    }

    // ── Missing "name" claim → UserId works based on sub claim ──────────────

    [Fact]
    public void UserId_HasSubClaimButNoNameClaim_ReturnsSubClaimValue()
    {
        // Arrange
        const string userId = "user-abc-123";
        var identity = new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.NameIdentifier, userId) },
            "TestAuth");
        var user = new ClaimsPrincipal(identity);

        var service = CreateService(user);

        // Act & Assert
        service.UserId.ShouldBe(userId);
    }

    // ── Missing admin/role claims → Roles returns empty ─────────────────────

    [Fact]
    public void Roles_NoRoleClaims_ReturnsEmptyList()
    {
        // Arrange — authenticated user with no role claims
        var identity = new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.NameIdentifier, "user-1") },
            "TestAuth");
        var user = new ClaimsPrincipal(identity);

        var service = CreateService(user);

        // Act & Assert
        service.Roles.ShouldBeEmpty();
    }

    [Fact]
    public void Roles_NullHttpContext_ReturnsEmptyList()
    {
        var service = CreateServiceWithNoHttpContext();
        service.Roles.ShouldBeEmpty();
    }

    [Fact]
    public void Roles_UnauthenticatedUser_ReturnsEmptyList()
    {
        // Arrange — no authenticated identity
        var service = CreateService();

        // Act & Assert
        service.Roles.ShouldBeEmpty();
    }

    // ── Valid admin/role claims ──────────────────────────────────────────────

    [Fact]
    public void Roles_WithStandardRoleClaims_ReturnsRoles()
    {
        // Arrange
        var identity = new ClaimsIdentity(
            new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "user-1"),
                new Claim(ClaimTypes.Role, "admin"),
                new Claim(ClaimTypes.Role, "user"),
            },
            "TestAuth");
        var user = new ClaimsPrincipal(identity);

        var service = CreateService(user);

        // Act
        var roles = service.Roles;

        // Assert
        roles.Count.ShouldBe(2);
        roles.ShouldContain("admin");
        roles.ShouldContain("user");
    }

    // ── Keycloak realm_access.roles fallback ────────────────────────────────

    [Fact]
    public void Roles_WithKeycloakRealmAccessClaim_ReturnsRoles()
    {
        // Arrange
        var realmAccess = JsonSerializer.Serialize(new { roles = new[] { "admin", "moderator" } });
        var identity = new ClaimsIdentity(
            new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "user-1"),
                new Claim("realm_access", realmAccess),
            },
            "TestAuth");
        var user = new ClaimsPrincipal(identity);

        var service = CreateService(user);

        // Act
        var roles = service.Roles;

        // Assert
        roles.Count.ShouldBe(2);
        roles.ShouldContain("admin");
        roles.ShouldContain("moderator");
    }

    [Fact]
    public void Roles_WithMalformedRealmAccessClaim_ReturnsEmpty()
    {
        // Arrange — invalid JSON in realm_access
        var identity = new ClaimsIdentity(
            new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "user-1"),
                new Claim("realm_access", "this-is-not-json"),
            },
            "TestAuth");
        var user = new ClaimsPrincipal(identity);

        var service = CreateService(user);

        // Act & Assert — should handle gracefully without throwing
        service.Roles.ShouldBeEmpty();
    }

    [Fact]
    public void Roles_WithRealmAccessMissingRolesProperty_ReturnsEmpty()
    {
        // Arrange — valid JSON but no "roles" property
        var realmAccess = JsonSerializer.Serialize(new { permissions = new[] { "read", "write" } });
        var identity = new ClaimsIdentity(
            new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "user-1"),
                new Claim("realm_access", realmAccess),
            },
            "TestAuth");
        var user = new ClaimsPrincipal(identity);

        var service = CreateService(user);

        // Act & Assert
        service.Roles.ShouldBeEmpty();
    }

    [Fact]
    public void Roles_WithRealmAccessRolesNotArray_ReturnsEmpty()
    {
        // Arrange — "roles" exists but is not an array
        var realmAccess = JsonSerializer.Serialize(new { roles = "admin" });
        var identity = new ClaimsIdentity(
            new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "user-1"),
                new Claim("realm_access", realmAccess),
            },
            "TestAuth");
        var user = new ClaimsPrincipal(identity);

        var service = CreateService(user);

        // Act & Assert
        service.Roles.ShouldBeEmpty();
    }

    // ── Standard role claims take precedence over realm_access ───────────────

    [Fact]
    public void Roles_WithBothStandardAndKeycloakClaims_StandardTakesPrecedence()
    {
        // Arrange
        var realmAccess = JsonSerializer.Serialize(new { roles = new[] { "keycloak-role" } });
        var identity = new ClaimsIdentity(
            new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "user-1"),
                new Claim(ClaimTypes.Role, "standard-role"),
                new Claim("realm_access", realmAccess),
            },
            "TestAuth");
        var user = new ClaimsPrincipal(identity);

        var service = CreateService(user);

        // Act
        var roles = service.Roles;

        // Assert — standard roles used, keycloak fallback NOT used
        roles.Count.ShouldBe(1);
        roles.ShouldContain("standard-role");
        roles.ShouldNotContain("keycloak-role");
    }

    // ── Realm access with non-string items in roles array ───────────────────

    [Fact]
    public void Roles_WithRealmAccessContainingNonStringRoles_FiltersToStringsOnly()
    {
        // Arrange — JSON array with mixed types: strings and numbers
        const string realmAccess = """{"roles": ["admin", 123, "user", null, true]}""";
        var identity = new ClaimsIdentity(
            new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "user-1"),
                new Claim("realm_access", realmAccess),
            },
            "TestAuth");
        var user = new ClaimsPrincipal(identity);

        var service = CreateService(user);

        // Act
        var roles = service.Roles;

        // Assert — only string values should be included
        roles.ShouldContain("admin");
        roles.ShouldContain("user");
    }
}
