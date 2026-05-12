using Wolverine;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Application.Models.Commands;
using nem.Mimir.Application.Models.Queries;
using nem.Mimir.Domain.ValueObjects;

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
        var models = await _bus.InvokeAsync<IReadOnlyList<LlmModelInfoDto>>(new GetModelsQuery(), cancellationToken).ConfigureAwait(false);
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
        var model = await _bus.InvokeAsync<LlmModelInfoDto?>(new GetModelStatusQuery(modelId), cancellationToken).ConfigureAwait(false);

        if (model is null)
        {
            return NotFound();
        }

        return Ok(model);
    }

    [HttpGet("profiles")]
    [ProducesResponseType(typeof(IReadOnlyList<ModelProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetProfiles(CancellationToken cancellationToken)
    {
        var profiles = await _bus.InvokeAsync<IReadOnlyList<ModelProfileDto>>(new GetModelProfilesQuery(), cancellationToken).ConfigureAwait(false);
        return Ok(profiles);
    }

    [HttpPost("profiles")]
    [ProducesResponseType(typeof(ModelProfileDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CreateProfile([FromBody] CreateModelProfileRequest request, CancellationToken cancellationToken)
    {
        var profile = await _bus.InvokeAsync<ModelProfileDto>(
            new CreateModelProfileCommand(
                request.Name,
                request.ModelId,
                request.Temperature,
                request.TopP,
                request.MaxTokens,
                request.FrequencyPenalty,
                request.PresencePenalty,
                request.StopSequences,
                request.SystemPromptOverride,
                request.ResponseFormat),
            cancellationToken).ConfigureAwait(false);

        return CreatedAtAction(nameof(GetProfiles), new { id = profile.Id }, profile);
    }

    [HttpPut("profiles/{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateProfile(ModelProfileId id, [FromBody] UpdateModelProfileRequest request, CancellationToken cancellationToken)
    {
        await _bus.InvokeAsync(
            new UpdateModelProfileCommand(
                id,
                request.Name,
                request.ModelId,
                request.Temperature,
                request.TopP,
                request.MaxTokens,
                request.FrequencyPenalty,
                request.PresencePenalty,
                request.StopSequences,
                request.SystemPromptOverride,
                request.ResponseFormat),
            cancellationToken).ConfigureAwait(false);

        return NoContent();
    }

    [HttpDelete("profiles/{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteProfile(ModelProfileId id, CancellationToken cancellationToken)
    {
        await _bus.InvokeAsync(new DeleteModelProfileCommand(id), cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    [HttpGet("{modelId}/capabilities")]
    [ProducesResponseType(typeof(ModelCapabilityDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetModelCapabilities(string modelId, CancellationToken cancellationToken)
    {
        var capabilities = await _bus.InvokeAsync<ModelCapabilityDto>(new GetModelCapabilitiesQuery(modelId), cancellationToken).ConfigureAwait(false);
        return Ok(capabilities);
    }

    [HttpGet("arena-config")]
    [ProducesResponseType(typeof(ArenaConfigDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetArenaConfig(CancellationToken cancellationToken)
    {
        var config = await _bus.InvokeAsync<ArenaConfigDto>(new GetArenaConfigQuery(), cancellationToken).ConfigureAwait(false);
        return Ok(config);
    }

    [HttpPut("arena-config")]
    [ProducesResponseType(typeof(ArenaConfigDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SetArenaConfig([FromBody] SetArenaConfigRequest request, CancellationToken cancellationToken)
    {
        var config = await _bus.InvokeAsync<ArenaConfigDto>(
            new SetArenaConfigCommand(
                request.ModelIds,
                request.IsBlindComparisonEnabled,
                request.ShowModelNamesAfterVote),
            cancellationToken).ConfigureAwait(false);

        return Ok(config);
    }
}

public sealed record CreateModelProfileRequest(
    string Name,
    string ModelId,
    decimal? Temperature,
    decimal? TopP,
    int? MaxTokens,
    decimal? FrequencyPenalty,
    decimal? PresencePenalty,
    IReadOnlyList<string>? StopSequences,
    string? SystemPromptOverride,
    string? ResponseFormat);

public sealed record UpdateModelProfileRequest(
    string Name,
    string ModelId,
    decimal? Temperature,
    decimal? TopP,
    int? MaxTokens,
    decimal? FrequencyPenalty,
    decimal? PresencePenalty,
    IReadOnlyList<string>? StopSequences,
    string? SystemPromptOverride,
    string? ResponseFormat);

public sealed record SetArenaConfigRequest(
    IReadOnlyList<string> ModelIds,
    bool IsBlindComparisonEnabled,
    bool ShowModelNamesAfterVote);
