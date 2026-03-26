using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Application.Notes.Commands;
using nem.Mimir.Application.Notes.Queries;
using Wolverine;

namespace nem.Mimir.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public sealed class NotesController : ControllerBase
{
    private readonly IMessageBus _bus;

    public NotesController(IMessageBus bus)
    {
        _bus = bus;
    }

    [HttpPost]
    [ProducesResponseType(typeof(NoteDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateNoteRequest request, CancellationToken ct)
    {
        var result = await _bus.InvokeAsync<NoteDto>(
            new CreateNoteCommand(request.Title, request.Content, request.Tags),
            ct);

        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpGet]
    [ProducesResponseType(typeof(PaginatedList<NoteListDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _bus.InvokeAsync<PaginatedList<NoteListDto>>(new GetNotesQuery(pageNumber, pageSize), ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(NoteDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _bus.InvokeAsync<NoteDto>(new GetNoteByIdQuery(id), ct);
        return Ok(result);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateNoteRequest request, CancellationToken ct)
    {
        await _bus.InvokeAsync(
            new UpdateNoteCommand(id, request.Title, request.Content, request.ChangeDescription, request.Tags),
            ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _bus.InvokeAsync(new DeleteNoteCommand(id), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/collaborators")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> AddCollaborator(Guid id, [FromBody] AddCollaboratorRequest request, CancellationToken ct)
    {
        await _bus.InvokeAsync(new AddCollaboratorCommand(id, request.UserId, request.Permission), ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}/collaborators/{userId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemoveCollaborator(Guid id, Guid userId, CancellationToken ct)
    {
        await _bus.InvokeAsync(new RemoveCollaboratorCommand(id, userId), ct);
        return NoContent();
    }

    [HttpGet("{id:guid}/versions")]
    [ProducesResponseType(typeof(PaginatedList<NoteVersionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetVersions(
        Guid id,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _bus.InvokeAsync<PaginatedList<NoteVersionDto>>(
            new GetNoteVersionsQuery(id, pageNumber, pageSize),
            ct);

        return Ok(result);
    }

    [HttpPost("{id:guid}/versions/{versionId:guid}/restore")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RestoreVersion(Guid id, Guid versionId, CancellationToken ct)
    {
        await _bus.InvokeAsync(new RestoreNoteVersionCommand(id, versionId), ct);
        return NoContent();
    }
}

public sealed record CreateNoteRequest(string Title, byte[] Content, IReadOnlyList<string> Tags);

public sealed record UpdateNoteRequest(string Title, byte[] Content, string? ChangeDescription, IReadOnlyList<string> Tags);

public sealed record AddCollaboratorRequest(Guid UserId, string Permission);
