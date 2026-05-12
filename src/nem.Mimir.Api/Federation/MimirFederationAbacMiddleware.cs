using System.Text.Json;
using nem.Contracts.AspNetCore.Messaging.Federation;
using nem.Contracts.AspNetCore.Wolverine;
using nem.Contracts.Federation.Authorization;
using Wolverine;

namespace nem.Mimir.Api.Federation;

public sealed class MimirFederationAbacMiddleware(ILogger<MimirFederationAbacMiddleware> logger)
{
    public const string AuthorizationContextHeaderName = "X-Nem-Authorization-Context";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public void Before(Envelope envelope, MimirFederationAbacContextAccessor accessor)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(accessor);

        if (!IsFederationEnvelope(envelope))
        {
            return;
        }

        if (!envelope.Headers.TryGetValue(AuthorizationContextHeaderName, out var serializedContext)
            || string.IsNullOrWhiteSpace(serializedContext))
        {
            logger.LogWarning(
                "Federation ABAC context missing for message {MessageType}; continuing with graceful degradation.",
                envelope.MessageType ?? envelope.Message?.GetType().Name ?? "unknown");
            return;
        }

        try
        {
            var context = JsonSerializer.Deserialize<ClassificationAuthorizationContext>(serializedContext, JsonOptions);
            if (context is null)
            {
                logger.LogWarning(
                    "Federation ABAC context could not be deserialized for message {MessageType}; continuing with graceful degradation.",
                    envelope.MessageType ?? envelope.Message?.GetType().Name ?? "unknown");
                return;
            }

            accessor.Current = context;
        }
        catch (JsonException ex)
        {
            logger.LogWarning(
                ex,
                "Federation ABAC context was invalid JSON for message {MessageType}; continuing with graceful degradation.",
                envelope.MessageType ?? envelope.Message?.GetType().Name ?? "unknown");
        }
    }

    public void After(MimirFederationAbacContextAccessor accessor)
    {
        ArgumentNullException.ThrowIfNull(accessor);
        accessor.Current = null;
    }

    private static bool IsFederationEnvelope(Envelope envelope)
        => envelope.Headers.ContainsKey(TenantPropagationSendingMiddleware.TenantIdHeader)
           || envelope.Headers.ContainsKey(FederationCorrelationHeaderNames.SourceTenantId)
           || envelope.Headers.ContainsKey(FederationCorrelationHeaderNames.TargetTenantId)
           || envelope.Headers.ContainsKey(FederationCorrelationHeaderNames.TraceParent);
}
