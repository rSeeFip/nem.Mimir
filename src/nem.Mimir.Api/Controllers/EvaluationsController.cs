using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Application.Evaluations.Commands;
using nem.Mimir.Application.Evaluations.Queries;
using Wolverine;

namespace nem.Mimir.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public sealed class EvaluationsController : ControllerBase
{
    private readonly IMessageBus _bus;

    public EvaluationsController(IMessageBus bus)
    {
        _bus = bus;
    }

    [HttpPost]
    [ProducesResponseType(typeof(EvaluationDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateEvaluationRequest request, CancellationToken ct)
    {
        var result = await _bus.InvokeAsync<EvaluationDto>(
            new CreateEvaluationCommand(
                request.ModelAId,
                request.ModelBId,
                request.Prompt,
                request.ResponseA,
                request.ResponseB),
            ct);

        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPost("{id:guid}/result")]
    [ProducesResponseType(typeof(EvaluationDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> SubmitResult(Guid id, [FromBody] SubmitEvaluationResultRequest request, CancellationToken ct)
    {
        var result = await _bus.InvokeAsync<EvaluationDto>(
            new SubmitEvaluationResultCommand(id, request.Winner),
            ct);

        return Ok(result);
    }

    [HttpGet("leaderboard")]
    [ProducesResponseType(typeof(PaginatedList<LeaderboardEntryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLeaderboard(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _bus.InvokeAsync<PaginatedList<LeaderboardEntryDto>>(
            new GetLeaderboardQuery(pageNumber, pageSize),
            ct);

        return Ok(result);
    }

    [HttpPost("feedback")]
    [ProducesResponseType(typeof(EvaluationFeedbackDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> SubmitFeedback([FromBody] SubmitFeedbackRequest request, CancellationToken ct)
    {
        var result = await _bus.InvokeAsync<EvaluationFeedbackDto>(
            new SubmitFeedbackCommand(
                request.EvaluationId,
                request.Quality,
                request.Relevance,
                request.Accuracy,
                request.Comment),
            ct);

        return Ok(result);
    }

    [HttpGet("history")]
    [ProducesResponseType(typeof(PaginatedList<EvaluationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHistory(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _bus.InvokeAsync<PaginatedList<EvaluationDto>>(
            new GetEvaluationHistoryQuery(pageNumber, pageSize),
            ct);

        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(EvaluationDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _bus.InvokeAsync<EvaluationDto>(new GetEvaluationByIdQuery(id), ct);
        return Ok(result);
    }
}

public sealed record CreateEvaluationRequest(
    Guid ModelAId,
    Guid ModelBId,
    string Prompt,
    string ResponseA,
    string ResponseB);

public sealed record SubmitEvaluationResultRequest(string Winner);

public sealed record SubmitFeedbackRequest(
    Guid EvaluationId,
    int Quality,
    int Relevance,
    int Accuracy,
    string? Comment);
