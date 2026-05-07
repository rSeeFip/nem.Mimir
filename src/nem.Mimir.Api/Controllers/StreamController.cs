using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using nem.Mimir.Domain.MultiTenancy;

namespace nem.Mimir.Api.Controllers;

[ApiController]
[Route("api/chat")]
[Authorize]
public sealed class StreamController : ControllerBase
{
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<StreamController> _logger;

    public StreamController(ITenantContext tenantContext, ILogger<StreamController> logger)
    {
        _tenantContext = tenantContext;
        _logger = logger;
    }

    [HttpGet("stream")]
    public async Task Stream(
        [FromQuery] Guid conversationId,
        CancellationToken ct)
    {
        Response.Headers.Append("Content-Type", "text/event-stream");
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("X-Accel-Buffering", "no");

        var lastEventId = Request.Headers["Last-Event-ID"].FirstOrDefault();
        var tenantId = _tenantContext.TenantId;

        _logger.LogInformation(
            "SSE stream started for conversation {ConversationId}, tenant {TenantId}, resuming from {LastEventId}",
            conversationId, tenantId, lastEventId ?? "start");

        var eventId = 0;

        try
        {
            await foreach (var chunk in GetStreamChunksAsync(conversationId, lastEventId, ct))
            {
                eventId++;
                var sseEvent = $"id: {eventId}\ndata: {chunk}\n\n";
                await Response.WriteAsync(sseEvent, ct);
                await Response.Body.FlushAsync(ct);
            }

            await Response.WriteAsync("data: [DONE]\n\n", ct);
            await Response.Body.FlushAsync(ct);
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("SSE stream cancelled for conversation {ConversationId}", conversationId);
        }
    }

    private static async IAsyncEnumerable<string> GetStreamChunksAsync(
        Guid conversationId,
        string? resumeFromEventId,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        await Task.Yield();
        yield break;
    }
}
