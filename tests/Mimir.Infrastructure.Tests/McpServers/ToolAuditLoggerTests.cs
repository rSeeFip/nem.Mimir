using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mimir.Domain.McpServers;
using Mimir.Infrastructure.McpServers;
using Mimir.Infrastructure.Persistence;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;

namespace Mimir.Infrastructure.Tests.McpServers;

public sealed class ToolAuditLoggerTests
{
    [Fact]
    public void SanitizeInput_RedactsBearerToken()
    {
        var input = """{"headers": {"Authorization": "Bearer eyJhbGciOiJIUzI1NiJ9.secret"}}""";

        var result = ToolAuditLogger.SanitizeInput(input);

        result.ShouldNotBeNull();
        result.ShouldNotContain("eyJhbGciOiJIUzI1NiJ9.secret");
        result.ShouldContain("[REDACTED]");
    }

    [Fact]
    public void SanitizeInput_RedactsApiKey()
    {
        var input = """{"api_key": "sk-abc123xyz"}""";

        var result = ToolAuditLogger.SanitizeInput(input);

        result.ShouldNotBeNull();
        result.ShouldNotContain("sk-abc123xyz");
        result.ShouldContain("[REDACTED]");
    }

    [Fact]
    public void SanitizeInput_RedactsPassword()
    {
        var input = """{"password": "super-secret-pass!"}""";

        var result = ToolAuditLogger.SanitizeInput(input);

        result.ShouldNotBeNull();
        result.ShouldNotContain("super-secret-pass!");
        result.ShouldContain("[REDACTED]");
    }

    [Fact]
    public void TruncateOutput_TruncatesLargeOutput()
    {
        var largeOutput = new string('x', 20_000);

        var result = ToolAuditLogger.TruncateOutput(largeOutput);

        result.ShouldNotBeNull();
        result.Length.ShouldBeLessThanOrEqualTo(10240);
        result.ShouldEndWith("[TRUNCATED at 10KB]");
    }

    [Fact]
    public void TruncateOutput_LeavesSmallOutputUnchanged()
    {
        var smallOutput = "hello world";

        var result = ToolAuditLogger.TruncateOutput(smallOutput);

        result.ShouldBe("hello world");
    }

    [Fact]
    public void SanitizeInput_ReturnsNull_ForNullInput()
    {
        var result = ToolAuditLogger.SanitizeInput(null);

        result.ShouldBeNull();
    }

    [Fact]
    public void TruncateOutput_ReturnsNull_ForNullOutput()
    {
        var result = ToolAuditLogger.TruncateOutput(null);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task LogToolExecutionAsync_DoesNotThrowOnDbError()
    {
        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        var scope = Substitute.For<IServiceScope>();
        var serviceProvider = Substitute.For<IServiceProvider>();
        scopeFactory.CreateScope().Returns(scope);
        scope.ServiceProvider.Returns(serviceProvider);
        serviceProvider.GetService(typeof(MimirDbContext))
            .Throws(new InvalidOperationException("DB unavailable"));
        var logger = Substitute.For<ILogger<ToolAuditLogger>>();

        var sut = new ToolAuditLogger(scopeFactory, logger);

        var entry = new McpToolAuditLog
        {
            Id = Guid.NewGuid(),
            ToolName = "test-tool",
            Timestamp = DateTime.UtcNow,
        };

        await Should.NotThrowAsync(() => sut.LogToolExecutionAsync(entry));
    }
}
