namespace Mimir.WhatsApp.Services;

using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mimir.WhatsApp.Configuration;
using Mimir.WhatsApp.Models;

internal static class WhatsAppWebhookEndpoints
{
    public static IEndpointRouteBuilder MapWhatsAppWebhook(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/webhook/whatsapp");

        group.MapGet("/", HandleVerification);
        group.MapPost("/", HandleIncoming);

        return endpoints;
    }

    internal static IResult HandleVerification(
        [FromQuery(Name = "hub.mode")] string? mode,
        [FromQuery(Name = "hub.verify_token")] string? verifyToken,
        [FromQuery(Name = "hub.challenge")] string? challenge,
        [FromServices] IOptions<WhatsAppSettings> settings,
        [FromServices] ILogger<WhatsAppChannelAdapter> logger)
    {
        if (mode == "subscribe" && verifyToken == settings.Value.VerifyToken)
        {
            logger.LogInformation("WhatsApp webhook verification succeeded");
            return Results.Ok(challenge);
        }

        logger.LogWarning("WhatsApp webhook verification failed: mode={Mode}", mode);
        return Results.StatusCode(403);
    }

    internal static async Task<IResult> HandleIncoming(
        HttpContext context,
        [FromServices] WhatsAppChannelAdapter adapter,
        [FromServices] IOptions<WhatsAppSettings> settings,
        [FromServices] ILogger<WhatsAppChannelAdapter> logger)
    {
        context.Request.EnableBuffering();
        var body = await new StreamReader(context.Request.Body).ReadToEndAsync();

        if (!string.IsNullOrWhiteSpace(settings.Value.AppSecret))
        {
            var signature = context.Request.Headers["X-Hub-Signature-256"].FirstOrDefault() ?? string.Empty;
            if (!WhatsAppSignatureValidator.IsValid(body, signature, settings.Value.AppSecret))
            {
                logger.LogWarning("WhatsApp webhook signature validation failed");
                return Results.StatusCode(403);
            }
        }

        WhatsAppWebhookPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<WhatsAppWebhookPayload>(body);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Failed to deserialize WhatsApp webhook payload");
            return Results.BadRequest();
        }

        if (payload is null)
            return Results.BadRequest();

        var ct = context.RequestAborted;
        await adapter.ProcessWebhookPayloadAsync(payload, ct);

        return Results.Ok();
    }
}
