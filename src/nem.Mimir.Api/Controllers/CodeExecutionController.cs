using Wolverine;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using nem.Mimir.Application.CodeExecution.Commands;

namespace nem.Mimir.Api.Controllers;

/// <summary>
/// Provides endpoints for executing code in a sandboxed Docker container within a conversation.
/// </summary>
[ApiController]
[Route("api/conversations/{conversationId:guid}/code-execution")]
[Authorize]
[Produces("application/json")]
public sealed class CodeExecutionController : ControllerBase
{
    private readonly IMessageBus _bus;

    /// <summary>
    /// Initializes a new instance of the <see cref="CodeExecutionController"/> class.
    /// </summary>
    /// <param name="bus">Wolverine message bus for dispatching commands.</param>
    public CodeExecutionController(IMessageBus bus)
    {
        _bus = bus;
    }

    /// <summary>
    /// Execute code in a sandboxed container.
    /// </summary>
    /// <param name="conversationId">The unique identifier of the conversation.</param>
    /// <param name="request">The code execution request containing language and code.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The execution result containing stdout, stderr, exit code, and timing information.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(CodeExecutionResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CodeExecutionResultDto>> Execute(
        Guid conversationId,
        [FromBody] ExecuteCodeRequest request,
        CancellationToken ct)
    {
        var result = await _bus.InvokeAsync<CodeExecutionResultDto>(
            new ExecuteCodeCommand(request.Language, request.Code, conversationId), ct);

        return Ok(result);
    }
}

/// <summary>
/// Request body for executing code in a sandboxed container.
/// </summary>
/// <param name="Language">The programming language ("python" or "javascript").</param>
/// <param name="Code">The source code to execute.</param>
public sealed record ExecuteCodeRequest(string Language, string Code);
