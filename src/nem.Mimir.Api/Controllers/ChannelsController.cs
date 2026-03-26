using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using nem.Mimir.Application.Channels.Commands;
using nem.Mimir.Application.Channels.Queries;
using nem.Mimir.Application.Common.Models;
using Wolverine;

namespace nem.Mimir.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public sealed class ChannelsController : ControllerBase
{
    private readonly IMessageBus _bus;

    public ChannelsController(IMessageBus bus)
    {
        _bus = bus;
    }

    [HttpPost]
    [ProducesResponseType(typeof(ChannelDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateChannelRequest request, CancellationToken ct)
    {
        var result = await _bus.InvokeAsync<ChannelDto>(
            new CreateChannelCommand(request.Name, request.Description, request.Type),
            ct);

        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpGet]
    [ProducesResponseType(typeof(PaginatedList<ChannelListDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _bus.InvokeAsync<PaginatedList<ChannelListDto>>(new GetChannelsQuery(pageNumber, pageSize), ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ChannelDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _bus.InvokeAsync<ChannelDto>(new GetChannelByIdQuery(id), ct);
        return Ok(result);
    }

    [HttpGet("related/{conversationId:guid}")]
    [ProducesResponseType(typeof(IReadOnlyList<ChannelListDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRelatedChannels(Guid conversationId, CancellationToken ct)
    {
        var result = await _bus.InvokeAsync<IReadOnlyList<ChannelListDto>>(new GetRelatedChannelsQuery(conversationId), ct);
        return Ok(result);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateChannelRequest request, CancellationToken ct)
    {
        await _bus.InvokeAsync(new UpdateChannelCommand(id, request.Name, request.Description), ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _bus.InvokeAsync(new DeleteChannelCommand(id), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/join")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Join(Guid id, CancellationToken ct)
    {
        await _bus.InvokeAsync(new JoinChannelCommand(id), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/leave")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Leave(Guid id, CancellationToken ct)
    {
        await _bus.InvokeAsync(new LeaveChannelCommand(id), ct);
        return NoContent();
    }

    [HttpGet("{id:guid}/members")]
    [ProducesResponseType(typeof(IReadOnlyList<ChannelMemberDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMembers(Guid id, CancellationToken ct)
    {
        var result = await _bus.InvokeAsync<IReadOnlyList<ChannelMemberDto>>(new GetChannelMembersQuery(id), ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}/messages")]
    [ProducesResponseType(typeof(PaginatedList<ChannelMessageDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMessages(
        Guid id,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var result = await _bus.InvokeAsync<PaginatedList<ChannelMessageDto>>(
            new GetChannelMessagesQuery(id, pageNumber, pageSize),
            ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/messages")]
    [ProducesResponseType(typeof(ChannelMessageDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> SendMessage(Guid id, [FromBody] SendChannelMessageRequest request, CancellationToken ct)
    {
        var result = await _bus.InvokeAsync<ChannelMessageDto>(
            new SendChannelMessageCommand(id, request.Content, request.ParentMessageId),
            ct);

        return CreatedAtAction(nameof(GetMessages), new { id }, result);
    }

    [HttpPut("{channelId:guid}/messages/{messageId:guid}")]
    [ProducesResponseType(typeof(ChannelMessageDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> EditMessage(
        Guid channelId,
        Guid messageId,
        [FromBody] EditChannelMessageRequest request,
        CancellationToken ct)
    {
        var result = await _bus.InvokeAsync<ChannelMessageDto>(
            new EditChannelMessageCommand(channelId, messageId, request.Content),
            ct);

        return Ok(result);
    }

    [HttpDelete("{channelId:guid}/messages/{messageId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteMessage(Guid channelId, Guid messageId, CancellationToken ct)
    {
        await _bus.InvokeAsync(new DeleteChannelMessageCommand(channelId, messageId), ct);
        return NoContent();
    }

    [HttpPost("{channelId:guid}/messages/{messageId:guid}/reactions")]
    [ProducesResponseType(typeof(ChannelMessageDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> ReactToMessage(
        Guid channelId,
        Guid messageId,
        [FromBody] ReactToChannelMessageRequest request,
        CancellationToken ct)
    {
        var result = await _bus.InvokeAsync<ChannelMessageDto>(
            new ReactToChannelMessageCommand(channelId, messageId, request.Emoji),
            ct);

        return Ok(result);
    }
}

public sealed record CreateChannelRequest(string Name, string? Description, string Type);

public sealed record UpdateChannelRequest(string Name, string? Description);

public sealed record SendChannelMessageRequest(string Content, Guid? ParentMessageId);

public sealed record EditChannelMessageRequest(string Content);

public sealed record ReactToChannelMessageRequest(string Emoji);
