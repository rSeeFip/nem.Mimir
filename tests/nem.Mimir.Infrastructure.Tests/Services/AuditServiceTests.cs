using Microsoft.AspNetCore.Http;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Domain.Entities;
using nem.Mimir.Infrastructure.Services;
using NSubstitute;
using Shouldly;

namespace nem.Mimir.Infrastructure.Tests.Services;

public sealed class AuditServiceTests
{
    private readonly IAuditRepository _auditRepository = Substitute.For<IAuditRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IHttpContextAccessor _httpContextAccessor = Substitute.For<IHttpContextAccessor>();

    private AuditService CreateService()
    {
        return new AuditService(_auditRepository, _unitOfWork, _httpContextAccessor);
    }

    // ── Audit entry created on action ───────────────────────────────────────

    [Fact]
    public async Task LogAsync_ValidInput_CreatesAuditEntryAndSaves()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _auditRepository
            .CreateAsync(Arg.Any<AuditEntry>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<AuditEntry>());
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);

        var service = CreateService();

        // Act
        await service.LogAsync(userId, "Login", "User", "user-123", "User logged in");

        // Assert
        await _auditRepository.Received(1).CreateAsync(
            Arg.Is<AuditEntry>(e =>
                e.UserId == userId &&
                e.Action == "Login" &&
                e.EntityType == "User"),
            Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // ── Null HTTP context → IP address is null (no crash) ───────────────────

    [Fact]
    public async Task LogAsync_NullHttpContext_DoesNotThrow()
    {
        // Arrange
        _httpContextAccessor.HttpContext.Returns((HttpContext?)null);
        _auditRepository
            .CreateAsync(Arg.Any<AuditEntry>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<AuditEntry>());
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);

        var service = CreateService();

        // Act & Assert — should not throw
        await service.LogAsync(Guid.NewGuid(), "Action", "Entity");
    }

    [Fact]
    public async Task LogAsync_NullHttpContext_CreatesEntryWithNullIpAddress()
    {
        // Arrange
        _httpContextAccessor.HttpContext.Returns((HttpContext?)null);
        AuditEntry? capturedEntry = null;
        _auditRepository
            .CreateAsync(Arg.Any<AuditEntry>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedEntry = callInfo.Arg<AuditEntry>();
                return capturedEntry;
            });
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);

        var service = CreateService();

        // Act
        await service.LogAsync(Guid.NewGuid(), "TestAction", "TestEntity");

        // Assert
        capturedEntry.ShouldNotBeNull();
        capturedEntry.IpAddress.ShouldBeNull();
    }

    // ── Empty GUID userId → AuditEntry.Create throws (domain validation) ────

    [Fact]
    public async Task LogAsync_EmptyGuidUserId_ThrowsArgumentException()
    {
        // Arrange
        var service = CreateService();

        // Act & Assert — AuditEntry.Create validates that userId != Guid.Empty
        await Should.ThrowAsync<ArgumentException>(
            () => service.LogAsync(Guid.Empty, "Action", "Entity"));
    }

    // ── Null/empty action → AuditEntry.Create throws ────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task LogAsync_NullOrEmptyAction_ThrowsArgumentException(string? action)
    {
        // Arrange
        var service = CreateService();

        // Act & Assert
        await Should.ThrowAsync<ArgumentException>(
            () => service.LogAsync(Guid.NewGuid(), action!, "Entity"));
    }

    // ── Null/empty entityType → AuditEntry.Create throws ────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task LogAsync_NullOrEmptyEntityType_ThrowsArgumentException(string? entityType)
    {
        // Arrange
        var service = CreateService();

        // Act & Assert
        await Should.ThrowAsync<ArgumentException>(
            () => service.LogAsync(Guid.NewGuid(), "Action", entityType!));
    }

    // ── Optional parameters (entityId, details) can be null ─────────────────

    [Fact]
    public async Task LogAsync_NullEntityIdAndDetails_DoesNotThrow()
    {
        // Arrange
        _auditRepository
            .CreateAsync(Arg.Any<AuditEntry>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<AuditEntry>());
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);

        var service = CreateService();

        // Act & Assert
        await service.LogAsync(Guid.NewGuid(), "Delete", "Conversation", entityId: null, details: null);
    }

    // ── Cancellation token is propagated ────────────────────────────────────

    [Fact]
    public async Task LogAsync_CancellationToken_PropagatedToRepository()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        _auditRepository
            .CreateAsync(Arg.Any<AuditEntry>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<AuditEntry>());
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);

        var service = CreateService();

        // Act
        await service.LogAsync(Guid.NewGuid(), "Action", "Entity", cancellationToken: cts.Token);

        // Assert
        await _auditRepository.Received(1).CreateAsync(Arg.Any<AuditEntry>(), cts.Token);
        await _unitOfWork.Received(1).SaveChangesAsync(cts.Token);
    }

    // ── IP address extracted from HttpContext when available ─────────────────

    [Fact]
    public async Task LogAsync_WithHttpContext_ExtractsIpAddress()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("192.168.1.100");
        _httpContextAccessor.HttpContext.Returns(httpContext);

        AuditEntry? capturedEntry = null;
        _auditRepository
            .CreateAsync(Arg.Any<AuditEntry>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedEntry = callInfo.Arg<AuditEntry>();
                return capturedEntry;
            });
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);

        var service = CreateService();

        // Act
        await service.LogAsync(Guid.NewGuid(), "Login", "User");

        // Assert
        capturedEntry.ShouldNotBeNull();
        capturedEntry.IpAddress.ShouldBe("192.168.1.100");
    }

    // ── HttpContext with null RemoteIpAddress → null IP in entry ─────────────

    [Fact]
    public async Task LogAsync_HttpContextWithNullRemoteIp_EntryHasNullIpAddress()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        // RemoteIpAddress is null by default on DefaultHttpContext
        _httpContextAccessor.HttpContext.Returns(httpContext);

        AuditEntry? capturedEntry = null;
        _auditRepository
            .CreateAsync(Arg.Any<AuditEntry>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedEntry = callInfo.Arg<AuditEntry>();
                return capturedEntry;
            });
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);

        var service = CreateService();

        // Act
        await service.LogAsync(Guid.NewGuid(), "Action", "Entity");

        // Assert
        capturedEntry.ShouldNotBeNull();
        capturedEntry.IpAddress.ShouldBeNull();
    }
}
