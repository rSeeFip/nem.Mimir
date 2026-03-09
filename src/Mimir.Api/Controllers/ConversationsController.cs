using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mimir.Application.Common.Models;
using Mimir.Application.Conversations.Commands;
using Mimir.Application.Conversations.Queries;

namespace Mimir.Api.Controllers;

/// <summary>
/// Manages conversations — create, retrieve, update, archive and delete.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public sealed class ConversationsController : ControllerBase
{
    private readonly ISender _sender;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConversationsController"/> class.
    /// </summary>
    /// <param name="sender">MediatR sender for dispatching commands and queries.</param>
    public ConversationsController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Creates a new conversation.
    /// </summary>
    /// <param name="request">The conversation creation payload.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The newly created conversation.</returns>
    /// <response code="201">Returns the newly created conversation with its ID and initial status.</response>
    /// <response code="400">If the request payload is invalid.</response>
    /// <response code="401">If the request is not authenticated.</response>
    [HttpPost]
    [ProducesResponseType(typeof(ConversationDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Create([FromBody] CreateConversationRequest request, CancellationToken ct)
    {
        var result = await _sender.Send(
            new CreateConversationCommand(request.Title, request.SystemPrompt, request.Model), ct);

        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>
    /// Retrieves a paginated list of conversations for the authenticated user.
    /// </summary>
    /// <param name="pageNumber">Page number (default 1).</param>
    /// <param name="pageSize">Page size (default 20).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A paginated list of conversation summaries.</returns>
    /// <response code="200">Returns the paginated list of conversation summaries.</response>
    /// <response code="401">If the request is not authenticated.</response>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedList<ConversationListDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetConversationsByUserQuery(pageNumber, pageSize), ct);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves a single conversation by its identifier, including messages.
    /// </summary>
    /// <param name="id">The conversation identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The conversation with its messages.</returns>
    /// <response code="200">Returns the full conversation details including messages.</response>
    /// <response code="401">If the request is not authenticated.</response>
    /// <response code="404">If the conversation was not found or doesn't belong to the user.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ConversationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _sender.Send(new GetConversationByIdQuery(id), ct);
        return Ok(result);
    }

    /// <summary>
    /// Updates the title of an existing conversation.
    /// </summary>
    /// <param name="id">The conversation identifier.</param>
    /// <param name="request">The new title payload.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="204">If the title was successfully updated.</response>
    /// <response code="400">If the new title is invalid.</response>
    /// <response code="401">If the request is not authenticated.</response>
    /// <response code="404">If the conversation was not found.</response>
    [HttpPut("{id:guid}/title")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateTitle(Guid id, [FromBody] UpdateConversationTitleRequest request, CancellationToken ct)
    {
        await _sender.Send(new UpdateConversationTitleCommand(id, request.Title), ct);
        return NoContent();
    }

    /// <summary>
    /// Archives a conversation, hiding it from the default list.
    /// </summary>
    /// <param name="id">The conversation identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost("{id:guid}/archive")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct)
    {
        await _sender.Send(new ArchiveConversationCommand(id), ct);
        return NoContent();
    }

    /// <summary>
    /// Permanently deletes a conversation and all its messages.
    /// </summary>
    /// <param name="id">The conversation identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _sender.Send(new DeleteConversationCommand(id), ct);
        return NoContent();
    }
}

/// <summary>
/// Request body for creating a new conversation.
/// </summary>
/// <param name="Title">The conversation title.</param>
/// <param name="SystemPrompt">Optional system prompt to prepend to the conversation.</param>
/// <param name="Model">Optional LLM model identifier to use for this conversation.</param>
public sealed record CreateConversationRequest(
    string Title,
    string? SystemPrompt,
    string? Model);

/// <summary>
/// Request body for updating a conversation's title.
/// </summary>
/// <param name="Title">The new title for the conversation.</param>
public sealed record UpdateConversationTitleRequest(string Title);
