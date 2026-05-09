using Wolverine;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Application.Memories.Commands;
using nem.Mimir.Application.Memories.Queries;
using nem.Mimir.Domain.ValueObjects;

namespace nem.Mimir.Api.Controllers;

[ApiController]
[Route("api/memories")]
[Authorize]
[Produces("application/json")]
public sealed class MemoriesController : ControllerBase
{
    private readonly IMessageBus _bus;

    public MemoriesController(IMessageBus bus)
    {
        _bus = bus;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<UserMemoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var result = await _bus.InvokeAsync<IReadOnlyList<UserMemoryDto>>(new GetUserMemoriesQuery(), ct).ConfigureAwait(false);
        return Ok(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(UserMemoryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Create([FromBody] CreateUserMemoryRequest request, CancellationToken ct)
    {
        var result = await _bus.InvokeAsync<UserMemoryDto>(
                new CreateUserMemoryCommand(request.Content, request.Context),
                ct)
            .ConfigureAwait(false);

        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(UserMemoryId id, [FromBody] UpdateUserMemoryRequest request, CancellationToken ct)
    {
        await _bus.InvokeAsync(new UpdateUserMemoryCommand(id, request.Content, request.Context), ct).ConfigureAwait(false);
        return NoContent();
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(UserMemoryId id, CancellationToken ct)
    {
        await _bus.InvokeAsync(new DeleteUserMemoryCommand(id), ct).ConfigureAwait(false);
        return NoContent();
    }
}

public sealed record CreateUserMemoryRequest(
    string Content,
    string? Context);

public sealed record UpdateUserMemoryRequest(
    string Content,
    string? Context);
