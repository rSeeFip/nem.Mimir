using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using nem.Mimir.Application.Billing;
using nem.Mimir.Application.Common.Interfaces;

namespace nem.Mimir.Api.Controllers;

/// <summary>
/// Provides tenant billing and usage query endpoints.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public sealed class BillingController : ControllerBase
{
    private readonly ITenantUsageQueryService _usageQueryService;
    private readonly ICurrentUserService _currentUserService;

    /// <summary>
    /// Initializes a new instance of the <see cref="BillingController"/> class.
    /// </summary>
    /// <param name="usageQueryService">Service for querying tenant usage data.</param>
    /// <param name="currentUserService">Service for accessing the current authenticated user.</param>
    public BillingController(
        ITenantUsageQueryService usageQueryService,
        ICurrentUserService currentUserService)
    {
        _usageQueryService = usageQueryService;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// Returns the usage summary for the current user's tenant over the specified date range.
    /// </summary>
    /// <param name="from">Start of the date range (inclusive).</param>
    /// <param name="to">End of the date range (exclusive).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="TenantUsageSummary"/> for the requested period.</returns>
    [HttpGet("usage")]
    [ProducesResponseType(typeof(TenantUsageSummary), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetUsage(
        [FromQuery] DateTimeOffset from,
        [FromQuery] DateTimeOffset to,
        CancellationToken ct = default)
    {
        var tenantId = _currentUserService.TenantId ?? string.Empty;
        var summary = await _usageQueryService.GetUsageAsync(tenantId, from, to, ct);
        return Ok(summary);
    }

    /// <summary>
    /// Returns the per-model usage breakdown for the current user's tenant over the specified date range.
    /// </summary>
    /// <param name="from">Start of the date range (inclusive).</param>
    /// <param name="to">End of the date range (exclusive).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A dictionary of model identifiers to <see cref="ModelUsage"/> entries.</returns>
    [HttpGet("usage/models")]
    [ProducesResponseType(typeof(Dictionary<string, ModelUsage>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetUsageByModel(
        [FromQuery] DateTimeOffset from,
        [FromQuery] DateTimeOffset to,
        CancellationToken ct = default)
    {
        var tenantId = _currentUserService.TenantId ?? string.Empty;
        var summary = await _usageQueryService.GetUsageAsync(tenantId, from, to, ct);
        return Ok(summary.ModelBreakdown);
    }
}
