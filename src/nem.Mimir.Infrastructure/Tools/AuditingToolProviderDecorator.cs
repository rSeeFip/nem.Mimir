namespace nem.Mimir.Infrastructure.Tools;

using System.Diagnostics;
using nem.Mimir.Domain.McpServers;
using nem.Mimir.Domain.Tools;

public sealed class AuditingToolProviderDecorator : IToolProvider
{
    private readonly IToolProvider _inner;
    private readonly IToolAuditLogger _auditLogger;

    public AuditingToolProviderDecorator(IToolProvider inner, IToolAuditLogger auditLogger)
    {
        _inner = inner;
        _auditLogger = auditLogger;
    }

    public Task<IReadOnlyList<ToolDefinition>> GetAvailableToolsAsync(CancellationToken cancellationToken = default)
        => _inner.GetAvailableToolsAsync(cancellationToken);

    public async Task<ToolResult> ExecuteToolAsync(string toolName, string argumentsJson, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var result = await _inner.ExecuteToolAsync(toolName, argumentsJson, cancellationToken);
            stopwatch.Stop();

            _ = _auditLogger.LogToolExecutionAsync(new McpToolAuditLog
            {
                Id = Guid.NewGuid(),
                ToolName = toolName,
                Input = argumentsJson,
                Output = result.Content,
                LatencyMs = stopwatch.ElapsedMilliseconds,
                Success = result.IsSuccess,
                ErrorMessage = result.ErrorMessage,
                Timestamp = DateTime.UtcNow,
            });

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            _ = _auditLogger.LogToolExecutionAsync(new McpToolAuditLog
            {
                Id = Guid.NewGuid(),
                ToolName = toolName,
                Input = argumentsJson,
                LatencyMs = stopwatch.ElapsedMilliseconds,
                Success = false,
                ErrorMessage = ex.Message,
                Timestamp = DateTime.UtcNow,
            });

            throw;
        }
    }
}
