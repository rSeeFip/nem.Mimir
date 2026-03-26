using Wolverine;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Application.Conversations.Commands;
using nem.Mimir.Application.Conversations.Queries;

namespace nem.Mimir.Api.Controllers;

/// <summary>
/// Manages conversations — create, retrieve, update, archive and delete.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public sealed class ConversationsController : ControllerBase
{
    private readonly IMessageBus _bus;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConversationsController"/> class.
    /// </summary>
    /// <param name="bus">Wolverine message bus for dispatching commands and queries.</param>
    public ConversationsController(IMessageBus bus)
    {
        _bus = bus;
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
        var result = await _bus.InvokeAsync<ConversationDto>(
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
        var result = await _bus.InvokeAsync<PaginatedList<ConversationListDto>>(new GetConversationsByUserQuery(pageNumber, pageSize), ct);
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
        var result = await _bus.InvokeAsync<ConversationDto>(new GetConversationByIdQuery(id), ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/fork")]
    [ProducesResponseType(typeof(ConversationDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Fork(Guid id, [FromBody] ForkConversationRequest request, CancellationToken ct)
    {
        var result = await _bus.InvokeAsync<ConversationDto>(
            new ForkConversationCommand(id, request.ForkReason), ct);

        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpGet("{id:guid}/lineage")]
    [ProducesResponseType(typeof(LineageGraphDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLineage(Guid id, CancellationToken ct)
    {
        var result = await _bus.InvokeAsync<LineageGraphDto>(new GetLineageGraphQuery(id), ct);
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
        await _bus.InvokeAsync(new UpdateConversationTitleCommand(id, request.Title), ct);
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
        await _bus.InvokeAsync(new ArchiveConversationCommand(id), ct);
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
        await _bus.InvokeAsync(new DeleteConversationCommand(id), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/pin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Pin(Guid id, CancellationToken ct)
    {
        await _bus.InvokeAsync(new PinConversationCommand(id), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/unpin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Unpin(Guid id, CancellationToken ct)
    {
        await _bus.InvokeAsync(new UnpinConversationCommand(id), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/share")]
    [ProducesResponseType(typeof(ConversationShareDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Share(Guid id, CancellationToken ct)
    {
        var result = await _bus.InvokeAsync<ConversationShareDto>(new ShareConversationCommand(id), ct);
        return Ok(result);
    }

    [HttpDelete("{id:guid}/share")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Unshare(Guid id, CancellationToken ct)
    {
        await _bus.InvokeAsync(new UnshareConversationCommand(id), ct);
        return NoContent();
    }

    [HttpPut("{id:guid}/tags")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Tag(Guid id, [FromBody] TagConversationRequest request, CancellationToken ct)
    {
        await _bus.InvokeAsync(new TagConversationCommand(id, request.Tag), ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}/tags/{tag}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Untag(Guid id, string tag, CancellationToken ct)
    {
        await _bus.InvokeAsync(new UntagConversationCommand(id, tag), ct);
        return NoContent();
    }

    [HttpPut("{id:guid}/folder")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MoveToFolder(Guid id, [FromBody] MoveConversationToFolderRequest request, CancellationToken ct)
    {
        await _bus.InvokeAsync(new MoveConversationToFolderCommand(id, request.FolderId), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/messages/{msgId:guid}/regenerate")]
    [ProducesResponseType(typeof(MessageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RegenerateMessage(Guid id, Guid msgId, [FromBody] RegenerateMessageRequest? request, CancellationToken ct)
    {
        var result = await _bus.InvokeAsync<MessageDto>(new RegenerateMessageCommand(id, msgId, request?.Model), ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/messages/{msgId:guid}/reactions")]
    [ProducesResponseType(typeof(MessageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddReaction(Guid id, Guid msgId, [FromBody] MessageReactionRequest request, CancellationToken ct)
    {
        var result = await _bus.InvokeAsync<MessageDto>(new AddReactionCommand(id, msgId, request.Emoji), ct);
        return Ok(result);
    }

    [HttpDelete("{id:guid}/messages/{msgId:guid}/reactions/{emoji}")]
    [ProducesResponseType(typeof(MessageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveReaction(Guid id, Guid msgId, string emoji, CancellationToken ct)
    {
        var result = await _bus.InvokeAsync<MessageDto>(new RemoveReactionCommand(id, msgId, emoji), ct);
        return Ok(result);
    }

    [HttpPut("{id:guid}/messages/{msgId:guid}")]
    [ProducesResponseType(typeof(MessageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> EditMessage(Guid id, Guid msgId, [FromBody] EditMessageRequest request, CancellationToken ct)
    {
        var result = await _bus.InvokeAsync<MessageDto>(new EditMessageCommand(id, msgId, request.Content), ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/messages/{msgId:guid}/branch/{branchIndex:int}")]
    [ProducesResponseType(typeof(MessageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SwitchBranch(Guid id, Guid msgId, int branchIndex, CancellationToken ct)
    {
        var result = await _bus.InvokeAsync<MessageDto>(new SwitchBranchCommand(id, msgId, branchIndex), ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/attach-file")]
    [RequestSizeLimit(50 * 1024 * 1024)]
    [ProducesResponseType(typeof(ConversationAttachmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> AttachFile(Guid id, [FromForm] IFormFile file, CancellationToken ct)
    {
        if (file.Length <= 0)
        {
            return BadRequest("File is required.");
        }

        await using var stream = file.OpenReadStream();
        using var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream, ct);

        var result = await _bus.InvokeAsync<ConversationAttachmentDto>(
            new AttachFileToConversationCommand(id, file.FileName, file.ContentType, memoryStream.ToArray()), ct);

        return Ok(result);
    }

    [HttpGet("{id:guid}/rag-context")]
    [ProducesResponseType(typeof(ConversationContextDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRagContext(Guid id, [FromQuery] string query, [FromQuery] int maxResults = 10, CancellationToken ct = default)
    {
        var result = await _bus.InvokeAsync<ConversationContextDto>(new GetConversationContextQuery(id, query, maxResults), ct);
        return Ok(result);
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

public sealed record ForkConversationRequest(string? ForkReason);

public sealed record TagConversationRequest(string Tag);

public sealed record MoveConversationToFolderRequest(Guid? FolderId);

public sealed record RegenerateMessageRequest(string? Model);

public sealed record MessageReactionRequest(string Emoji);

public sealed record EditMessageRequest(string Content);
