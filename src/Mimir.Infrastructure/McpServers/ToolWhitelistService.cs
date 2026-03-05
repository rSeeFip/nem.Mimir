namespace Mimir.Infrastructure.McpServers;

using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Mimir.Domain.McpServers;
using Mimir.Infrastructure.Persistence;

internal sealed partial class ToolWhitelistService(MimirDbContext context) : IToolWhitelistService
{
    private const int MaxArgumentSizeBytes = 10_240; // 10 KB

    public async Task<bool> IsToolAllowedAsync(
        Guid mcpServerConfigId,
        string toolName,
        CancellationToken cancellationToken = default)
    {
        var entry = await context.McpToolWhitelists
            .AsNoTracking()
            .FirstOrDefaultAsync(
                w => w.McpServerConfigId == mcpServerConfigId && w.ToolName == toolName,
                cancellationToken)
            .ConfigureAwait(false);

        // Default deny: if no whitelist entry exists, the tool is not allowed.
        return entry?.IsEnabled ?? false;
    }

    public async Task<IReadOnlyList<McpToolWhitelist>> GetAllowedToolsAsync(
        Guid mcpServerConfigId,
        CancellationToken cancellationToken = default)
    {
        return await context.McpToolWhitelists
            .AsNoTracking()
            .Where(w => w.McpServerConfigId == mcpServerConfigId && w.IsEnabled)
            .OrderBy(w => w.ToolName)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task SetToolWhitelistAsync(
        Guid mcpServerConfigId,
        string toolName,
        bool isEnabled,
        CancellationToken cancellationToken = default)
    {
        var existing = await context.McpToolWhitelists
            .FirstOrDefaultAsync(
                w => w.McpServerConfigId == mcpServerConfigId && w.ToolName == toolName,
                cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            existing.IsEnabled = isEnabled;
        }
        else
        {
            await context.McpToolWhitelists.AddAsync(
                new McpToolWhitelist
                {
                    Id = Guid.NewGuid(),
                    McpServerConfigId = mcpServerConfigId,
                    ToolName = toolName,
                    IsEnabled = isEnabled,
                    CreatedAt = DateTime.UtcNow,
                },
                cancellationToken).ConfigureAwait(false);
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public bool ValidateArguments(string argumentsJson)
    {
        if (string.IsNullOrEmpty(argumentsJson))
        {
            return true;
        }

        if (argumentsJson.Length > MaxArgumentSizeBytes)
        {
            return false;
        }

        if (PathTraversalPattern().IsMatch(argumentsJson))
        {
            return false;
        }

        if (SqlInjectionPattern().IsMatch(argumentsJson))
        {
            return false;
        }

        return true;
    }

    [GeneratedRegex(@"\.\./|\.\.\\|/etc/|/proc/|[A-Za-z]:\\", RegexOptions.Compiled)]
    private static partial Regex PathTraversalPattern();

    [GeneratedRegex(@"';\s*DROP|UNION\s+SELECT|OR\s+1\s*=\s*1|--", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex SqlInjectionPattern();
}
