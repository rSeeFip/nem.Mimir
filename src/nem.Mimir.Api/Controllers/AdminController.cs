using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using nem.Mimir.Application.Auditing.Queries;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Application.Admin.Commands;
using nem.Mimir.Application.Users.Commands;
using nem.Mimir.Application.Users.Queries;
using nem.Mimir.Domain.Enums;
using nem.Mimir.Domain.ValueObjects;

namespace nem.Mimir.Api.Controllers;

/// <summary>
/// Administrative endpoints for user management and audit log access.
/// All endpoints require the Admin role.
/// </summary>
[ApiController]
[Route("api/admin")]
[Authorize(Policy = "RequireAdmin")]
[Produces("application/json")]
public sealed class AdminController : ControllerBase
{
    private readonly ISender _sender;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdminController"/> class.
    /// </summary>
    /// <param name="sender">The MediatR sender for dispatching commands and queries.</param>
    public AdminController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Retrieves a paginated list of all users in the system.
    /// </summary>
    /// <param name="pageNumber">The page number to retrieve (default: 1).</param>
    /// <param name="pageSize">The number of items per page (default: 20).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A paginated list of users.</returns>
    /// <response code="200">Returns the paginated list of users.</response>
    /// <response code="401">The request is not authenticated.</response>
    /// <response code="403">The authenticated user does not have the Admin role.</response>
    [HttpGet("users")]
    [ProducesResponseType(typeof(PaginatedList<UserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAllUsers(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new GetAllUsersQuery(pageNumber, pageSize);
        var result = await _sender.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves a specific user by their unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the user.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The user details.</returns>
    /// <response code="200">Returns the user details.</response>
    /// <response code="401">The request is not authenticated.</response>
    /// <response code="403">The authenticated user does not have the Admin role.</response>
    [HttpGet("users/{id:guid}")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetUserById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var query = new GetUserByIdQuery(id);
        var result = await _sender.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Updates the role of a specific user.
    /// </summary>
    /// <param name="id">The unique identifier of the user whose role will be updated.</param>
    /// <param name="request">The request containing the new role.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>No content on success.</returns>
    /// <response code="204">The user's role was updated successfully.</response>
    /// <response code="401">The request is not authenticated.</response>
    /// <response code="403">The authenticated user does not have the Admin role.</response>
    [HttpPut("users/{id:guid}/role")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateUserRole(
        Guid id,
        [FromBody] UpdateUserRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<UserRole>(request.Role, ignoreCase: true, out var role))
        {
            return BadRequest($"Invalid role '{request.Role}'. Valid roles are: {string.Join(", ", Enum.GetNames<UserRole>())}.");
        }

        var command = new UpdateUserRoleCommand(id, role);
        await _sender.Send(command, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Deactivates a specific user account.
    /// </summary>
    /// <param name="id">The unique identifier of the user to deactivate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>No content on success.</returns>
    /// <response code="204">The user was deactivated successfully.</response>
    /// <response code="401">The request is not authenticated.</response>
    /// <response code="403">The authenticated user does not have the Admin role.</response>
    [HttpPost("users/{id:guid}/deactivate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeactivateUser(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var command = new DeactivateUserCommand(id);
        await _sender.Send(command, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Retrieves a paginated and filterable audit log.
    /// </summary>
    /// <param name="userId">Optional filter by user identifier.</param>
    /// <param name="action">Optional filter by action type.</param>
    /// <param name="from">Optional filter for entries from this timestamp onwards.</param>
    /// <param name="to">Optional filter for entries up to this timestamp.</param>
    /// <param name="pageNumber">The page number to retrieve (default: 1).</param>
    /// <param name="pageSize">The number of items per page (default: 20).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A paginated list of audit log entries.</returns>
    /// <response code="200">Returns the paginated audit log.</response>
    /// <response code="401">The request is not authenticated.</response>
    /// <response code="403">The authenticated user does not have the Admin role.</response>
    [HttpGet("audit")]
    [ProducesResponseType(typeof(PaginatedList<AuditEntryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAuditLog(
        [FromQuery] Guid? userId = null,
        [FromQuery] string? action = null,
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new GetAuditLogQuery(userId.HasValue ? UserId.From(userId.Value) : null, action, from, to, pageNumber, pageSize);
        var result = await _sender.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Restores a soft-deleted entity.
    /// </summary>
    /// <param name="entityType">The type of entity to restore (conversation, user, systemprompt).</param>
    /// <param name="id">The unique identifier of the entity to restore.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>No content on success.</returns>
    /// <response code="204">The entity was restored successfully.</response>
    /// <response code="400">The entity type is invalid or the entity is not soft-deleted.</response>
    /// <response code="401">The request is not authenticated.</response>
    /// <response code="403">The authenticated user does not have the Admin role.</response>
    /// <response code="404">The entity was not found.</response>
    [HttpPost("restore/{entityType}/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RestoreEntity(
        string entityType,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var command = new RestoreEntityCommand(entityType, id);
        await _sender.Send(command, cancellationToken);
        return NoContent();
    }
}

/// <summary>
/// Request body for updating a user's role.
/// </summary>
/// <param name="Role">The new role to assign to the user.</param>
public sealed record UpdateUserRoleRequest(string Role);
