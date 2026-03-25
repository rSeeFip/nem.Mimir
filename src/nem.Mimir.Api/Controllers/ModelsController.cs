using Wolverine;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Application.Models.Queries;

namespace nem.Mimir.Api.Controllers;

/// <summary>
/// Manages available LLM models and their status.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public sealed class ModelsController : ControllerBase
{
    private readonly IMessageBus _bus;
    private readonly ILogger<ModelsController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ModelsController"/> class.
    /// </summary>
    /// <param name="bus">Wolverine message bus for dispatching commands and queries.</param>
    /// <param name="logger">Logger instance.</param>
    public ModelsController(IMessageBus bus, ILogger<ModelsController> logger)
    {
        _bus = bus;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves a list of all available LLM models with their metadata.
    /// Results are cached for 60 seconds.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of available models.</returns>
    /// <response code="200">Returns the list of available models.</response>
    /// <response code="401">The request is not authenticated.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<LlmModelInfoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetModels(CancellationToken cancellationToken)
    {
        var models = await _bus.InvokeAsync<IReadOnlyList<LlmModelInfoDto>>(new GetModelsQuery(), cancellationToken);
        return Ok(models);
    }

    /// <summary>
    /// Retrieves the status of a specific LLM model.
    /// Results are cached for 60 seconds.
    /// </summary>
    /// <param name="modelId">The unique identifier of the model.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The model information if found.</returns>
    /// <response code="200">Returns the model information.</response>
    /// <response code="401">The request is not authenticated.</response>
    /// <response code="404">The model was not found.</response>
    [HttpGet("{modelId}/status")]
    [ProducesResponseType(typeof(LlmModelInfoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetModelStatus(string modelId, CancellationToken cancellationToken)
    {
        var model = await _bus.InvokeAsync<LlmModelInfoDto?>(new GetModelStatusQuery(modelId), cancellationToken);

        if (model is null)
        {
            return NotFound();
        }

        return Ok(model);
    }
}
