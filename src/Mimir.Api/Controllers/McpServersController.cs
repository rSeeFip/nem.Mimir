using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mimir.Application.McpServers.Commands;
using Mimir.Application.McpServers.Dtos;
using Mimir.Application.McpServers.Queries;
using Mimir.Domain.McpServers;

namespace Mimir.Api.Controllers;

/// <summary>
/// Manages MCP server configurations — CRUD, toggle, tools, whitelist, health, and audit logs.
/// </summary>
[ApiController]
[Route("api/mcp/servers")]
[Authorize(Policy = "RequireAdmin")]
[Produces("application/json")]
public sealed class McpServersController : ControllerBase
{
    private readonly ISender _sender;

    /// <summary>
    /// Initializes a new instance of the <see cref="McpServersController"/> class.
    /// </summary>
    /// <param name="sender">MediatR sender for dispatching commands and queries.</param>
    public McpServersController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Retrieves all MCP server configurations.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<McpServerDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var result = await _sender.Send(new GetMcpServersQuery(), ct);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves a single MCP server configuration by its identifier.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(McpServerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _sender.Send(new GetMcpServerByIdQuery(id), ct);
        return Ok(result);
    }

    /// <summary>
    /// Creates a new MCP server configuration.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create([FromBody] CreateMcpServerRequest request, CancellationToken ct)
    {
        var id = await _sender.Send(new CreateMcpServerCommand(
            request.Name,
            request.TransportType,
            request.Description,
            request.Command,
            request.Arguments,
            request.Url,
            request.EnvironmentVariablesJson,
            request.IsEnabled), ct);

        return CreatedAtAction(nameof(GetById), new { id }, id);
    }

    /// <summary>
    /// Updates an existing MCP server configuration.
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateMcpServerRequest request, CancellationToken ct)
    {
        await _sender.Send(new UpdateMcpServerCommand(
            id,
            request.Name,
            request.TransportType,
            request.Description,
            request.Command,
            request.Arguments,
            request.Url,
            request.EnvironmentVariablesJson), ct);

        return NoContent();
    }

    /// <summary>
    /// Deletes an MCP server configuration.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _sender.Send(new DeleteMcpServerCommand(id), ct);
        return NoContent();
    }

    /// <summary>
    /// Enables or disables an MCP server.
    /// </summary>
    [HttpPost("{id:guid}/toggle")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Toggle(Guid id, [FromBody] ToggleMcpServerRequest request, CancellationToken ct)
    {
        await _sender.Send(new ToggleMcpServerCommand(id, request.IsEnabled), ct);
        return NoContent();
    }

    /// <summary>
    /// Lists tools exposed by a connected MCP server.
    /// </summary>
    [HttpGet("{id:guid}/tools")]
    [ProducesResponseType(typeof(IReadOnlyList<McpServerToolDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTools(Guid id, CancellationToken ct)
    {
        var result = await _sender.Send(new GetMcpServerToolsQuery(id), ct);
        return Ok(result);
    }

    /// <summary>
    /// Updates the tool whitelist for an MCP server.
    /// </summary>
    [HttpPut("{id:guid}/whitelist")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateWhitelist(Guid id, [FromBody] UpdateToolWhitelistRequest request, CancellationToken ct)
    {
        await _sender.Send(new UpdateToolWhitelistCommand(id, request.Entries), ct);
        return NoContent();
    }

    /// <summary>
    /// Checks the health of a connected MCP server.
    /// </summary>
    [HttpGet("{id:guid}/health")]
    [ProducesResponseType(typeof(McpServerHealthDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetHealth(Guid id, CancellationToken ct)
    {
        var result = await _sender.Send(new GetMcpServerHealthQuery(id), ct);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves MCP tool audit logs, optionally filtered by server.
    /// </summary>
    [HttpGet("audit")]
    [ProducesResponseType(typeof(IReadOnlyList<McpAuditLogDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAuditLogs(
        [FromQuery] Guid? serverId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetMcpAuditLogsQuery(serverId, page, pageSize), ct);
        return Ok(result);
    }
}

/// <summary>
/// Request body for creating a new MCP server configuration.
/// </summary>
public sealed record CreateMcpServerRequest(
    string Name,
    McpTransportType TransportType,
    string? Description,
    string? Command,
    string? Arguments,
    string? Url,
    string? EnvironmentVariablesJson,
    bool IsEnabled = false);

/// <summary>
/// Request body for updating an MCP server configuration.
/// </summary>
public sealed record UpdateMcpServerRequest(
    string Name,
    McpTransportType TransportType,
    string? Description,
    string? Command,
    string? Arguments,
    string? Url,
    string? EnvironmentVariablesJson);

/// <summary>
/// Request body for toggling an MCP server's enabled state.
/// </summary>
public sealed record ToggleMcpServerRequest(bool IsEnabled);

/// <summary>
/// Request body for updating the tool whitelist of an MCP server.
/// </summary>
public sealed record UpdateToolWhitelistRequest(IReadOnlyList<ToolWhitelistEntry> Entries);
