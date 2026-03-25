using Wolverine;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Application.Conversations.Commands;
using nem.Mimir.Application.Conversations.Queries;

namespace nem.Mimir.Api.Controllers;

/// <summary>
/// Provides endpoints for sending messages and retrieving conversation message history.
/// </summary>
[ApiController]
[Route("api/conversations/{conversationId:guid}/messages")]
[Authorize]
[Produces("application/json")]
public sealed class MessagesController : ControllerBase
{
    private readonly IMessageBus _bus;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessagesController"/> class.
    /// </summary>
    /// <param name="bus">Wolverine message bus for dispatching commands and queries.</param>
    public MessagesController(IMessageBus bus)
    {
        _bus = bus;
    }

    /// <summary>
    /// Sends a message to the conversation and returns the assistant's response.
    /// </summary>
    /// <param name="conversationId">The unique identifier of the conversation.</param>
    /// <param name="request">The message content and optional model selection.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The assistant's response message.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(MessageDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Send(
        Guid conversationId,
        [FromBody] SendMessageRequest request,
        CancellationToken ct)
    {
        var result = await _bus.InvokeAsync<MessageDto>(
            new SendMessageCommand(conversationId, request.Content, request.Model), ct);

        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>
    /// Retrieves paginated message history for a conversation.
    /// </summary>
    /// <param name="conversationId">The unique identifier of the conversation.</param>
    /// <param name="pageNumber">The page number (default: 1).</param>
    /// <param name="pageSize">The number of messages per page (default: 20).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A paginated list of messages.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedList<MessageDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMessages(
        Guid conversationId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _bus.InvokeAsync<PaginatedList<MessageDto>>(
            new GetConversationMessagesQuery(conversationId, pageNumber, pageSize), ct);

        return Ok(result);
    }
}

/// <summary>
/// Request body for sending a message to a conversation.
/// </summary>
/// <param name="Content">The message content text.</param>
/// <param name="Model">The optional LLM model to use for generating the response.</param>
public sealed record SendMessageRequest(string Content, string? Model);
