using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using nem.Mimir.Application.Tenants.Commands;

namespace nem.Mimir.Api.Controllers;

/// <summary>
/// Tenant configuration endpoints accessible to tenant-admins.
/// </summary>
[ApiController]
[Route("api/tenants")]
[Authorize(Policy = "tenant-admin")]
[Produces("application/json")]
public sealed class TenantConfigurationController(ISender sender) : ControllerBase
{
    [HttpPut("{id}/configuration")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateConfiguration(string id, [FromBody] UpdateTenantConfigurationRequest request, CancellationToken cancellationToken)
    {
        await sender.Send(
            new UpdateTenantConfigurationCommand(id, request.RateLimitPerMinute, request.AllowedModels, request.AllowedTools, request.FeatureFlags),
            cancellationToken);
        return NoContent();
    }
}
