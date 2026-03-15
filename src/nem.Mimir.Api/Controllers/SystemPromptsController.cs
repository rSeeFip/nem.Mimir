using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Application.SystemPrompts.Commands;
using nem.Mimir.Application.SystemPrompts.Queries;
using nem.Mimir.Domain.ValueObjects;

namespace nem.Mimir.Api.Controllers;

/// <summary>
/// Manages system prompt templates — CRUD and rendering.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public sealed class SystemPromptsController : ControllerBase
{
    private readonly ISender _sender;

    /// <summary>
    /// Initializes a new instance of the <see cref="SystemPromptsController"/> class.
    /// </summary>
    /// <param name="sender">MediatR sender for dispatching commands and queries.</param>
    public SystemPromptsController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Creates a new system prompt.
    /// </summary>
    /// <param name="request">The system prompt creation payload.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The newly created system prompt.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(SystemPromptDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Create([FromBody] CreateSystemPromptRequest request, CancellationToken ct)
    {
        var result = await _sender.Send(
            new CreateSystemPromptCommand(request.Name, request.Template, request.Description ?? string.Empty), ct);

        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>
    /// Retrieves a paginated list of system prompts.
    /// </summary>
    /// <param name="pageNumber">Page number (default 1).</param>
    /// <param name="pageSize">Page size (default 20).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A paginated list of system prompts.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedList<SystemPromptDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _sender.Send(new ListSystemPromptsQuery(pageNumber, pageSize), ct);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves a single system prompt by its identifier.
    /// </summary>
    /// <param name="id">The system prompt identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The system prompt.</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(SystemPromptDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(SystemPromptId id, CancellationToken ct)
    {
        var result = await _sender.Send(new GetSystemPromptByIdQuery(id), ct);
        return Ok(result);
    }

    /// <summary>
    /// Updates an existing system prompt.
    /// </summary>
    /// <param name="id">The system prompt identifier.</param>
    /// <param name="request">The update payload.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(SystemPromptId id, [FromBody] UpdateSystemPromptRequest request, CancellationToken ct)
    {
        await _sender.Send(
            new UpdateSystemPromptCommand(id, request.Name, request.Template, request.Description ?? string.Empty), ct);
        return NoContent();
    }

    /// <summary>
    /// Permanently deletes a system prompt.
    /// </summary>
    /// <param name="id">The system prompt identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(SystemPromptId id, CancellationToken ct)
    {
        await _sender.Send(new DeleteSystemPromptCommand(id), ct);
        return NoContent();
    }

    /// <summary>
    /// Renders a system prompt template by replacing variables with provided values.
    /// </summary>
    /// <param name="id">The system prompt identifier.</param>
    /// <param name="request">The rendering payload with variable values.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The rendered prompt text.</returns>
    [HttpPost("{id}/render")]
    [ProducesResponseType(typeof(RenderSystemPromptResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Render(SystemPromptId id, [FromBody] RenderSystemPromptRequest request, CancellationToken ct)
    {
        var result = await _sender.Send(
            new RenderSystemPromptQuery(id, request.Variables ?? new Dictionary<string, string>()), ct);
        return Ok(new RenderSystemPromptResponse(result));
    }
}

/// <summary>
/// Request body for creating a new system prompt.
/// </summary>
/// <param name="Name">The system prompt name.</param>
/// <param name="Template">The template text with {{variable}} placeholders.</param>
/// <param name="Description">Optional description of the system prompt.</param>
public sealed record CreateSystemPromptRequest(
    string Name,
    string Template,
    string? Description);

/// <summary>
/// Request body for updating an existing system prompt.
/// </summary>
/// <param name="Name">The system prompt name.</param>
/// <param name="Template">The template text with {{variable}} placeholders.</param>
/// <param name="Description">Optional description of the system prompt.</param>
public sealed record UpdateSystemPromptRequest(
    string Name,
    string Template,
    string? Description);

/// <summary>
/// Request body for rendering a system prompt template.
/// </summary>
/// <param name="Variables">Dictionary of variable names to their values.</param>
public sealed record RenderSystemPromptRequest(
    IDictionary<string, string>? Variables);

/// <summary>
/// Response body for a rendered system prompt.
/// </summary>
/// <param name="RenderedText">The rendered prompt text with variables replaced.</param>
public sealed record RenderSystemPromptResponse(string RenderedText);
