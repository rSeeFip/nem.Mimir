using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Application.Images.Commands;
using nem.Mimir.Application.Images.Queries;
using Wolverine;

namespace nem.Mimir.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public sealed class ImagesController : ControllerBase
{
    private readonly IMessageBus _bus;

    public ImagesController(IMessageBus bus)
    {
        _bus = bus;
    }

    [HttpPost("generate")]
    [ProducesResponseType(typeof(ImageGenerationDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Generate([FromBody] GenerateImageRequest request, CancellationToken ct)
    {
        var result = await _bus.InvokeAsync<ImageGenerationDto>(
            new GenerateImageCommand(
                request.Prompt,
                request.NegativePrompt,
                request.Model,
                request.Size,
                request.Quality,
                request.NumberOfImages),
            ct);

        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpGet]
    [ProducesResponseType(typeof(PaginatedList<ImageGenerationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _bus.InvokeAsync<PaginatedList<ImageGenerationDto>>(
            new GetImageGenerationsQuery(pageNumber, pageSize),
            ct);

        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ImageGenerationDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _bus.InvokeAsync<ImageGenerationDto>(new GetImageByIdQuery(id), ct);
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _bus.InvokeAsync(new DeleteImageCommand(id), ct);
        return NoContent();
    }
}

public sealed record GenerateImageRequest(
    string Prompt,
    string? NegativePrompt,
    string Model,
    string Size,
    string? Quality,
    int NumberOfImages = 1);
