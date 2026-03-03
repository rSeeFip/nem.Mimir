using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Mimir.Application.Common.Interfaces;
using Mimir.Application.Common.Sanitization;
using Mimir.Domain.Entities;
using Mimir.Infrastructure.Services;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;

namespace Mimir.Infrastructure.Tests;

/// <summary>
/// Comprehensive negative tests for Infrastructure layer services.
/// Covers repository failures, exception propagation, and edge cases.
/// </summary>
public sealed class InfrastructureNegativeTests
{
    private readonly IAuditRepository _auditRepo = Substitute.For<IAuditRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly Microsoft.AspNetCore.Http.IHttpContextAccessor _httpContextAccessor =
        Substitute.For<Microsoft.AspNetCore.Http.IHttpContextAccessor>();

    // ══════════════════════════════════════════════════════════════════
    // AuditService — exception propagation from repository
    // ══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task AuditService_RepositoryThrows_PropagatesException()
    {
        // Arrange
        _auditRepo.CreateAsync(Arg.Any<AuditEntry>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("DB connection failed"));

        var service = new AuditService(_auditRepo, _unitOfWork, _httpContextAccessor);

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(
            () => service.LogAsync(Guid.NewGuid(), "Action", "Entity"));
    }

    [Fact]
    public async Task AuditService_UnitOfWorkThrows_PropagatesException()
    {
        // Arrange
        _auditRepo.CreateAsync(Arg.Any<AuditEntry>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<AuditEntry>());
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Concurrent modification"));

        var service = new AuditService(_auditRepo, _unitOfWork, _httpContextAccessor);

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(
            () => service.LogAsync(Guid.NewGuid(), "Action", "Entity"));
    }

    [Fact]
    public async Task AuditService_CancellationRequested_PropagatesCancellation()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        _auditRepo.CreateAsync(Arg.Any<AuditEntry>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());

        var service = new AuditService(_auditRepo, _unitOfWork, _httpContextAccessor);

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(
            () => service.LogAsync(Guid.NewGuid(), "Action", "Entity", cancellationToken: cts.Token));
    }

    // ══════════════════════════════════════════════════════════════════
    // AuditService — domain validation through service layer
    // ══════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AuditService_InvalidAction_ThrowsArgumentException(string? action)
    {
        var service = new AuditService(_auditRepo, _unitOfWork, _httpContextAccessor);

        await Should.ThrowAsync<ArgumentException>(
            () => service.LogAsync(Guid.NewGuid(), action!, "Entity"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AuditService_InvalidEntityType_ThrowsArgumentException(string? entityType)
    {
        var service = new AuditService(_auditRepo, _unitOfWork, _httpContextAccessor);

        await Should.ThrowAsync<ArgumentException>(
            () => service.LogAsync(Guid.NewGuid(), "Action", entityType!));
    }

    [Fact]
    public async Task AuditService_EmptyGuidUserId_ThrowsArgumentException()
    {
        var service = new AuditService(_auditRepo, _unitOfWork, _httpContextAccessor);

        await Should.ThrowAsync<ArgumentException>(
            () => service.LogAsync(Guid.Empty, "Action", "Entity"));
    }

    // ══════════════════════════════════════════════════════════════════
    // SanitizationService — edge cases
    // ══════════════════════════════════════════════════════════════════

    [Fact]
    public void SanitizationService_NullInput_DoesNotThrow()
    {
        var service = CreateSanitizationService();

        var result = service.ContainsSuspiciousPatterns(null!);

        // Should handle null gracefully — false or no crash
        result.ShouldBe(false);
    }

    [Fact]
    public void SanitizationService_EmptyInput_ReturnsFalse()
    {
        var service = CreateSanitizationService();

        var result = service.ContainsSuspiciousPatterns("");

        result.ShouldBeFalse();
    }

    [Fact]
    public void SanitizationService_XssScript_DetectsPattern()
    {
        var service = CreateSanitizationService();

        var result = service.ContainsSuspiciousPatterns("<script>alert('xss')</script>");

        result.ShouldBeTrue();
    }

    [Fact]
    public void SanitizationService_SqlInjection_DetectsPattern()
    {
        var service = CreateSanitizationService();

        var result = service.ContainsSuspiciousPatterns("'; DROP TABLE users; --");

        result.ShouldBeTrue();
    }

    [Fact]
    public void SanitizationService_NormalInput_ReturnsFalse()
    {
        var service = CreateSanitizationService();

        var result = service.ContainsSuspiciousPatterns("Hello, how are you today?");

        result.ShouldBeFalse();
    }

    private static SanitizationService CreateSanitizationService()
    {
        var settings = Options.Create(new SanitizationSettings { LogSuspiciousPatterns = true });
        var logger = NullLoggerFactory.Instance.CreateLogger<SanitizationService>();
        return new SanitizationService(settings, logger);
    }

    // ══════════════════════════════════════════════════════════════════
    // SystemPromptService — failure modes
    // ══════════════════════════════════════════════════════════════════

    [Fact]
    public void SystemPromptService_RenderTemplateWithNullTemplate_ThrowsArgumentException()
    {
        var service = new SystemPromptService();

        Should.Throw<ArgumentException>(() => service.RenderTemplate(null!, new Dictionary<string, string>()));
    }

    [Fact]
    public void SystemPromptService_RenderTemplateWithEmptyTemplate_ThrowsArgumentException()
    {
        var service = new SystemPromptService();

        Should.Throw<ArgumentException>(() => service.RenderTemplate("", new Dictionary<string, string>()));
    }

    [Fact]
    public void SystemPromptService_RenderTemplateWithVariableNotInTemplate_ReturnsTemplateUnchanged()
    {
        var service = new SystemPromptService();
        var variables = new Dictionary<string, string> { ["missing"] = "value" };

        var result = service.RenderTemplate("Hello {{name}}", variables);

        // Variable "missing" doesn't exist in template, "name" is not provided
        result.ShouldContain("{{name}}");
    }
}
