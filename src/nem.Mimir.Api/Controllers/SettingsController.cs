using Wolverine;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Application.Settings.Commands;
using nem.Mimir.Application.Settings.Queries;

namespace nem.Mimir.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public sealed class SettingsController : ControllerBase
{
    private readonly IMessageBus _bus;

    public SettingsController(IMessageBus bus)
    {
        _bus = bus;
    }

    [HttpGet]
    [ProducesResponseType(typeof(UserPreferenceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var result = await _bus.InvokeAsync<UserPreferenceDto>(new GetUserPreferencesQuery(), ct);
        return Ok(result);
    }

    [HttpGet("{section}")]
    [ProducesResponseType(typeof(UserPreferenceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBySection(string section, CancellationToken ct)
    {
        var result = await _bus.InvokeAsync<UserPreferenceDto>(new GetUserPreferencesBySectionQuery(section), ct);
        return Ok(result);
    }

    [HttpPut]
    [ProducesResponseType(typeof(UserPreferenceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Update([FromBody] UpdateUserPreferencesRequest request, CancellationToken ct)
    {
        var command = new UpdateUserPreferencesCommand(
            Section: null,
            Values: null,
            Settings: request.Settings);

        var result = await _bus.InvokeAsync<UserPreferenceDto>(command, ct);
        return Ok(result);
    }

    [HttpPut("{section}")]
    [ProducesResponseType(typeof(UserPreferenceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateSection(string section, [FromBody] UpdateUserPreferenceSectionRequest request, CancellationToken ct)
    {
        var command = new UpdateUserPreferencesCommand(
            Section: section,
            Values: request.Values,
            Settings: null);

        var result = await _bus.InvokeAsync<UserPreferenceDto>(command, ct);
        return Ok(result);
    }

    [HttpDelete]
    [ProducesResponseType(typeof(UserPreferenceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Reset(CancellationToken ct)
    {
        var result = await _bus.InvokeAsync<UserPreferenceDto>(new ResetUserPreferencesCommand(), ct);
        return Ok(result);
    }
}

public sealed record UpdateUserPreferencesRequest(
    Dictionary<string, Dictionary<string, object>> Settings);

public sealed record UpdateUserPreferenceSectionRequest(
    Dictionary<string, object> Values);
