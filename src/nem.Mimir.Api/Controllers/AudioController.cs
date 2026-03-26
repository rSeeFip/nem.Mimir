using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using nem.Contracts.Voice;
using nem.Mimir.Application.Audio.Commands;
using nem.Mimir.Application.Audio.Queries;
using Wolverine;

namespace nem.Mimir.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public sealed class AudioController : ControllerBase
{
    private readonly IMessageBus _bus;

    public AudioController(IMessageBus bus)
    {
        _bus = bus;
    }

    [HttpPost("transcribe")]
    [ProducesResponseType(typeof(TranscriptionDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Transcribe([FromForm] TranscribeAudioRequest request, CancellationToken ct)
    {
        if (request.Audio is null || request.Audio.Length == 0)
        {
            return BadRequest("Audio file is required.");
        }

        if (!AudioValidation.ValidateFormat(Path.GetExtension(request.Audio.FileName).TrimStart('.')))
        {
            return BadRequest("Unsupported audio format. Only wav is supported.");
        }

        await using var memoryStream = new MemoryStream();
        await request.Audio.CopyToAsync(memoryStream, ct);

        var result = await _bus.InvokeAsync<TranscriptionDto>(
            new TranscribeAudioCommand(memoryStream.ToArray(), request.LanguageHint),
            ct);

        return Ok(result);
    }

    [HttpPost("synthesize")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Synthesize([FromBody] SynthesizeSpeechRequest request, CancellationToken ct)
    {
        var audio = await _bus.InvokeAsync<byte[]>(
            new SynthesizeSpeechCommand(request.Text, request.Voice, request.Speed),
            ct);

        return File(audio, "audio/mpeg", "synthesized-audio.mp3");
    }

    [HttpPost("stream")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task Stream([FromBody] SynthesizeSpeechRequest request, [FromServices] ITextToSpeechProvider ttsProvider, CancellationToken ct)
    {
        Response.StatusCode = StatusCodes.Status200OK;
        Response.ContentType = "audio/mpeg";

        await foreach (var chunk in ttsProvider.SynthesizeAsync(request.Text, request.Voice, ct))
        {
            await Response.Body.WriteAsync(chunk, ct);
            await Response.Body.FlushAsync(ct);
        }
    }

    [HttpGet("voices")]
    [ProducesResponseType(typeof(VoiceOptionsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetVoices(CancellationToken ct)
    {
        var result = await _bus.InvokeAsync<VoiceOptionsDto>(new GetVoiceOptionsQuery(), ct);
        return Ok(result);
    }
}

public sealed record TranscribeAudioRequest(IFormFile Audio, string? LanguageHint);

public sealed record SynthesizeSpeechRequest(string Text, string? Voice, double Speed = 1.0);
