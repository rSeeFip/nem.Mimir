using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Application.Knowledge.Commands;
using nem.Mimir.Domain.ValueObjects;
using Wolverine;

namespace nem.Mimir.Api.Controllers;

[ApiController]
[Route("api/conversations/{id:guid}/files")]
[Authorize]
[Produces("application/json")]
public sealed class ConversationFilesController : ControllerBase
{
    private readonly IMessageBus _bus;

    public ConversationFilesController(IMessageBus bus)
    {
        _bus = bus;
    }

    [HttpPost]
    [RequestSizeLimit(50 * 1024 * 1024)]
    [ProducesResponseType(typeof(ChatFileUploadDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Upload(
        Guid id,
        [FromForm] IFormFile file,
        [FromForm] Guid? collectionId,
        CancellationToken ct)
    {
        if (file.Length <= 0)
        {
            return BadRequest("File is required.");
        }

        await using var stream = file.OpenReadStream();
        using var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream, ct);

        var command = new UploadChatFileCommand(
            id,
            file.FileName,
            file.ContentType,
            memoryStream.ToArray(),
            collectionId.HasValue ? KnowledgeCollectionId.From(collectionId.Value) : null);

        var result = await _bus.InvokeAsync<ChatFileUploadDto>(command, ct);
        return Ok(result);
    }
}
