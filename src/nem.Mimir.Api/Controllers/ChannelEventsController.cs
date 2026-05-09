using Wolverine;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using nem.Mimir.Application.ChannelEvents;

namespace nem.Mimir.Api.Controllers;

/// <summary>
/// Receives inbound channel events and dispatches outbound channel messages.
/// Requires service-to-service authentication.
/// </summary>
[ApiController]
[Route("api/channel-events")]
[Authorize(Policy = "ServiceAuth")]
[Produces("application/json")]
public sealed class ChannelEventsController : ControllerBase
{
    private readonly IMessageBus _bus;

    public ChannelEventsController(IMessageBus bus)
    {
        _bus = bus;
    }

    /// <summary>
    /// Receives an inbound message from a channel adapter.
    /// </summary>
    /// <param name="command">The inbound channel event to ingest.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The ingested event tracking information.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(ChannelEventResult), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> IngestEvent([FromBody] IngestChannelEventCommand command, CancellationToken ct)
    {
        var result = await _bus.InvokeAsync<ChannelEventResult>(command, ct);
        return Created(string.Empty, result);
    }

    /// <summary>
    /// Sends an outbound message to a channel adapter.
    /// </summary>
    /// <param name="command">The outbound message to send.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The delivery result.</returns>
    [HttpPost("send")]
    [ProducesResponseType(typeof(SendChannelMessageResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SendMessage([FromBody] SendChannelMessageCommand command, CancellationToken ct)
    {
        var result = await _bus.InvokeAsync<SendChannelMessageResult>(command, ct);
        return Ok(result);
    }
}
