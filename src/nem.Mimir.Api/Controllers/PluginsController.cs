using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using nem.Mimir.Application.Plugins.Commands;
using nem.Mimir.Application.Plugins.Queries;
using nem.Mimir.Domain.Plugins;

namespace nem.Mimir.Api.Controllers;

/// <summary>
/// Manages plugins — load, unload, list, and execute.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public sealed class PluginsController : ControllerBase
{
    private readonly ISender _sender;

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginsController"/> class.
    /// </summary>
    /// <param name="sender">MediatR sender for dispatching commands and queries.</param>
    public PluginsController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Loads a plugin from the specified assembly path.
    /// </summary>
    /// <param name="request">The plugin load payload.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The metadata of the loaded plugin.</returns>
    [Authorize(Policy = "RequireAdmin")]

    [HttpPost]
    [ProducesResponseType(typeof(PluginMetadata), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Load([FromBody] LoadPluginRequest request, CancellationToken ct)
    {
        var result = await _sender.Send(new LoadPluginCommand(request.AssemblyPath), ct);
        return CreatedAtAction(nameof(List), result);
    }

    /// <summary>
    /// Retrieves all currently loaded plugins.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of plugin metadata.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PluginMetadata>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var result = await _sender.Send(new ListPluginsQuery(), ct);
        return Ok(result);
    }

    /// <summary>
    /// Executes a loaded plugin with the given parameters.
    /// </summary>
    /// <param name="id">The plugin identifier.</param>
    /// <param name="request">The execution payload.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The plugin execution result.</returns>
    [HttpPost("{id}/execute")]
    [ProducesResponseType(typeof(PluginResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Execute(string id, [FromBody] ExecutePluginRequest request, CancellationToken ct)
    {
        var result = await _sender.Send(
            new ExecutePluginCommand(id, request.UserId, request.Parameters), ct);
        return Ok(result);
    }

    /// <summary>
    /// Unloads a plugin by its identifier.
    /// </summary>
    /// <param name="id">The plugin identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    [Authorize(Policy = "RequireAdmin")]

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Unload(string id, CancellationToken ct)
    {
        await _sender.Send(new UnloadPluginCommand(id), ct);
        return NoContent();
    }
}

/// <summary>
/// Request body for loading a plugin.
/// </summary>
/// <param name="AssemblyPath">The file path to the plugin assembly DLL.</param>
public sealed record LoadPluginRequest(string AssemblyPath);

/// <summary>
/// Request body for executing a plugin.
/// </summary>
/// <param name="UserId">The ID of the user executing the plugin.</param>
/// <param name="Parameters">Key-value parameters to pass to the plugin.</param>
public sealed record ExecutePluginRequest(
    string UserId,
    Dictionary<string, object> Parameters);
