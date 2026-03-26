using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Application.Knowledge.Queries;
using Wolverine;

namespace nem.Mimir.Api.Controllers;

[ApiController]
[Route("api/search")]
[Authorize]
[Produces("application/json")]
public sealed class SearchController : ControllerBase
{
    private readonly IMessageBus _bus;

    public SearchController(IMessageBus bus)
    {
        _bus = bus;
    }

    [HttpGet("web")]
    [ProducesResponseType(typeof(IReadOnlyList<WebSearchResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Web([FromQuery] string q, CancellationToken ct)
    {
        var result = await _bus.InvokeAsync<IReadOnlyList<WebSearchResultDto>>(new WebSearchQuery(q), ct);
        return Ok(result);
    }
}
