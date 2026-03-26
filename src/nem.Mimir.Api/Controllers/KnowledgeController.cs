using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Application.Knowledge.Commands;
using nem.Mimir.Application.Knowledge.Queries;
using nem.Mimir.Domain.ValueObjects;
using Wolverine;

namespace nem.Mimir.Api.Controllers;

[ApiController]
[Route("api/knowledge/collections")]
[Authorize]
[Produces("application/json")]
public sealed class KnowledgeController : ControllerBase
{
    private readonly IMessageBus _bus;

    public KnowledgeController(IMessageBus bus)
    {
        _bus = bus;
    }

    [HttpPost]
    [ProducesResponseType(typeof(KnowledgeCollectionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Create([FromBody] CreateKnowledgeCollectionRequest request, CancellationToken ct)
    {
        var result = await _bus.InvokeAsync<KnowledgeCollectionDto>(
            new CreateKnowledgeCollectionCommand(request.Name, request.Description),
            ct);

        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<KnowledgeCollectionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var result = await _bus.InvokeAsync<IReadOnlyList<KnowledgeCollectionDto>>(new GetCollectionsQuery(), ct);
        return Ok(result);
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(KnowledgeCollectionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(KnowledgeCollectionId id, CancellationToken ct)
    {
        var result = await _bus.InvokeAsync<KnowledgeCollectionDto>(new GetKnowledgeCollectionByIdQuery(id), ct);
        return Ok(result);
    }

    [HttpPut("{id}")]
    [ProducesResponseType(typeof(KnowledgeCollectionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(KnowledgeCollectionId id, [FromBody] UpdateKnowledgeCollectionRequest request, CancellationToken ct)
    {
        var result = await _bus.InvokeAsync<KnowledgeCollectionDto>(
            new UpdateKnowledgeCollectionCommand(id, request.Name, request.Description),
            ct);

        return Ok(result);
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(KnowledgeCollectionId id, CancellationToken ct)
    {
        await _bus.InvokeAsync(new DeleteKnowledgeCollectionCommand(id), ct);
        return NoContent();
    }

    [HttpPost("{id}/documents")]
    [ProducesResponseType(typeof(KnowledgeCollectionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddDocument(KnowledgeCollectionId id, [FromBody] AddDocumentToCollectionRequest request, CancellationToken ct)
    {
        var result = await _bus.InvokeAsync<KnowledgeCollectionDto>(
            new AddDocumentToCollectionCommand(id, request.DocumentId, request.FileName, request.StorageUrl, request.ContentType),
            ct);

        return Ok(result);
    }

    [HttpDelete("{id}/documents/{documentId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveDocument(KnowledgeCollectionId id, Guid documentId, CancellationToken ct)
    {
        await _bus.InvokeAsync(new RemoveDocumentCommand(id, documentId), ct);
        return NoContent();
    }

    [HttpGet("{id}/search")]
    [ProducesResponseType(typeof(IReadOnlyList<KnowledgeSearchResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Search(KnowledgeCollectionId id, [FromQuery] string q, [FromQuery] int maxResults = 10, CancellationToken ct = default)
    {
        var result = await _bus.InvokeAsync<IReadOnlyList<KnowledgeSearchResultDto>>(
            new SearchCollectionQuery(id, q, maxResults),
            ct);

        return Ok(result);
    }
}

public sealed record CreateKnowledgeCollectionRequest(string Name, string? Description);

public sealed record UpdateKnowledgeCollectionRequest(string Name, string? Description);

public sealed record AddDocumentToCollectionRequest(
    Guid DocumentId,
    string FileName,
    string StorageUrl,
    string? ContentType);
