namespace Mimir.Infrastructure.McpServers;

using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mimir.Domain.McpServers;
using Mimir.Infrastructure.Persistence;

internal sealed partial class ToolAuditLogger : IToolAuditLogger
{
    private const int MaxOutputLength = 10240;
    private const string TruncationSuffix = "[TRUNCATED at 10KB]";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ToolAuditLogger> _logger;

    public ToolAuditLogger(IServiceScopeFactory scopeFactory, ILogger<ToolAuditLogger> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task LogToolExecutionAsync(McpToolAuditLog entry, CancellationToken ct = default)
    {
        try
        {
            entry.Input = SanitizeInput(entry.Input);
            entry.Output = TruncateOutput(entry.Output);

            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MimirDbContext>();
            dbContext.McpToolAuditLogs.Add(entry);
            await dbContext.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist tool audit log for {ToolName}", entry.ToolName);
        }
    }

    internal static string? SanitizeInput(string? input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        var sanitized = BearerTokenPattern().Replace(input, "$1[REDACTED]");
        sanitized = ApiKeyPattern().Replace(sanitized, "$1[REDACTED]");
        sanitized = PasswordPattern().Replace(sanitized, "$1[REDACTED]");

        return sanitized;
    }

    internal static string? TruncateOutput(string? output)
    {
        if (output is null || output.Length <= MaxOutputLength)
            return output;

        return string.Concat(output.AsSpan(0, MaxOutputLength - TruncationSuffix.Length), TruncationSuffix);
    }

    [GeneratedRegex(@"(Bearer\s+)\S+", RegexOptions.IgnoreCase)]
    private static partial Regex BearerTokenPattern();

    [GeneratedRegex(@"(api[_\-]?key[""'\s:=]+)\S+", RegexOptions.IgnoreCase)]
    private static partial Regex ApiKeyPattern();

    [GeneratedRegex(@"(password[""'\s:=]+)\S+", RegexOptions.IgnoreCase)]
    private static partial Regex PasswordPattern();
}
