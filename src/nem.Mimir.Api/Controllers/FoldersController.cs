using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Application.Folders.Commands;
using nem.Mimir.Application.Folders.Queries;
using Wolverine;

namespace nem.Mimir.Api.Controllers;

[ApiController]
[Route("api/conversations/folders")]
[Authorize]
[Produces("application/json")]
public sealed class FoldersController : ControllerBase
{
    private readonly IMessageBus _bus;

    public FoldersController(IMessageBus bus)
    {
        _bus = bus;
    }

    [HttpPost]
    [ProducesResponseType(typeof(FolderDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Create([FromBody] CreateFolderRequest request, CancellationToken ct)
    {
        var result = await _bus.InvokeAsync<FolderDto>(new CreateFolderCommand(request.Name, request.ParentId), ct);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<FolderDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var result = await _bus.InvokeAsync<IReadOnlyList<FolderDto>>(new GetFoldersQuery(), ct);
        return Ok(result);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Rename(Guid id, [FromBody] RenameFolderRequest request, CancellationToken ct)
    {
        await _bus.InvokeAsync(new RenameFolderCommand(id, request.Name), ct);
        return NoContent();
    }

    [HttpPut("{id:guid}/parent")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Move(Guid id, [FromBody] MoveFolderRequest request, CancellationToken ct)
    {
        await _bus.InvokeAsync(new MoveFolderCommand(id, request.ParentId), ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _bus.InvokeAsync(new DeleteFolderCommand(id), ct);
        return NoContent();
    }
}

public sealed record CreateFolderRequest(string Name, Guid? ParentId);

public sealed record RenameFolderRequest(string Name);

public sealed record MoveFolderRequest(Guid? ParentId);
