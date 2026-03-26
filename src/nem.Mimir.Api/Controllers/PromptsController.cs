using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using nem.Contracts.Identity;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Application.Prompts.Commands;
using nem.Mimir.Application.Prompts.Queries;
using Wolverine;

namespace nem.Mimir.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public sealed class PromptsController : ControllerBase
{
    private readonly IMessageBus _bus;

    public PromptsController(IMessageBus bus)
    {
        _bus = bus;
    }

    [HttpPost]
    [ProducesResponseType(typeof(PromptTemplateDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Create([FromBody] CreatePromptTemplateRequest request, CancellationToken ct)
    {
        var result = await _bus.InvokeAsync<PromptTemplateDto>(
            new CreatePromptTemplateCommand(request.Title, request.Command, request.Content, request.Tags, request.IsShared),
            ct);

        return CreatedAtAction(nameof(GetVersionHistory), new { id = result.Id }, result);
    }

    [HttpGet]
    [ProducesResponseType(typeof(PaginatedList<PromptTemplateListDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] bool? isShared = null,
        [FromQuery] string[]? tags = null,
        CancellationToken ct = default)
    {
        var result = await _bus.InvokeAsync<PaginatedList<PromptTemplateListDto>>(
            new GetPromptTemplatesQuery(pageNumber, pageSize, search, isShared, tags),
            ct);

        return Ok(result);
    }

    [HttpGet("command/{command}")]
    [ProducesResponseType(typeof(PromptTemplateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByCommand(string command, CancellationToken ct)
    {
        var normalized = command.StartsWith('/') ? command : $"/{command}";
        var result = await _bus.InvokeAsync<PromptTemplateDto>(new GetPromptByCommandQuery(normalized), ct);
        return Ok(result);
    }

    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(PromptTemplateId id, [FromBody] UpdatePromptTemplateRequest request, CancellationToken ct)
    {
        await _bus.InvokeAsync(new UpdatePromptTemplateCommand(id, request.Title, request.Command, request.Content, request.Tags), ct);
        return NoContent();
    }

    [HttpPatch("{id}/share")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Share(PromptTemplateId id, [FromBody] SharePromptTemplateRequest request, CancellationToken ct)
    {
        await _bus.InvokeAsync(new SharePromptTemplateCommand(id, request.IsShared), ct);
        return NoContent();
    }

    [HttpGet("{id}/versions")]
    [ProducesResponseType(typeof(IReadOnlyList<PromptTemplateVersionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetVersionHistory(PromptTemplateId id, CancellationToken ct)
    {
        var result = await _bus.InvokeAsync<IReadOnlyList<PromptTemplateVersionDto>>(new GetPromptVersionHistoryQuery(id), ct);
        return Ok(result);
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(PromptTemplateId id, CancellationToken ct)
    {
        await _bus.InvokeAsync(new DeletePromptTemplateCommand(id), ct);
        return NoContent();
    }
}

public sealed record CreatePromptTemplateRequest(
    string Title,
    string Command,
    string Content,
    IReadOnlyList<string>? Tags,
    bool IsShared);

public sealed record UpdatePromptTemplateRequest(
    string Title,
    string Command,
    string Content,
    IReadOnlyList<string>? Tags);

public sealed record SharePromptTemplateRequest(bool IsShared);
