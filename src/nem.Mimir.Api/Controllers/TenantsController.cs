using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using nem.Mimir.Application.Tenants.Commands;
using nem.Mimir.Application.Tenants.Dtos;
using nem.Mimir.Application.Tenants.Queries;

namespace nem.Mimir.Api.Controllers;

/// <summary>
/// Tenant lifecycle management endpoints.
/// </summary>
[ApiController]
[Route("api/tenants")]
[Authorize(Policy = "platform-admin")]
[Produces("application/json")]
public sealed class TenantsController(ISender sender) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<TenantDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetTenantsQuery(), cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(TenantDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create([FromBody] OnboardTenantRequest request, CancellationToken cancellationToken)
    {
        var tenant = await sender.Send(
            new OnboardTenantCommand(request.Name, request.Slug, request.DefaultRateLimit),
            cancellationToken);

        return Created($"/api/tenants/{tenant.Id}", tenant);
    }

    [HttpPut("{id}/configuration")]
    [Authorize(Policy = "tenant-admin")]
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

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new OffboardTenantCommand(id), cancellationToken);
        return NoContent();
    }
}

public sealed record OnboardTenantRequest(string Name, string? Slug, int? DefaultRateLimit);
public sealed record UpdateTenantConfigurationRequest(int? RateLimitPerMinute, string[]? AllowedModels, string[]? AllowedTools, Dictionary<string, string>? FeatureFlags);
