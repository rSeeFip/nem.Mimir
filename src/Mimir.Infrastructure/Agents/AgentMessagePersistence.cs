namespace Mimir.Infrastructure.Agents;

using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mimir.Domain.Agents.Messages;
using Mimir.Domain.Entities;
using Mimir.Infrastructure.Persistence;

public sealed class AgentMessagePersistence : IAgentMessagePersistence
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly HashSet<string> SensitiveFieldNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "email",
        "ip",
        "ipaddress",
        "phone",
        "ssn",
        "token",
        "password",
    };

    private readonly IServiceScopeFactory _scopeFactory;

    public AgentMessagePersistence(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task PersistAsync(IAgentMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        var payload = JsonSerializer.Serialize(message, message.GetType(), JsonOptions);
        var sanitizedPayload = SanitizePayload(payload);
        var auditMessage = AgentMessage.Create(message, sanitizedPayload);

        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MimirDbContext>();

        var exists = await dbContext.AgentMessages.AnyAsync(x => x.Id == auditMessage.Id, cancellationToken).ConfigureAwait(false);

        if (exists)
        {
            return;
        }

        await dbContext.AgentMessages.AddAsync(auditMessage, cancellationToken).ConfigureAwait(false);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string SanitizePayload(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return payload;
        }

        JsonNode? node;
        try
        {
            node = JsonNode.Parse(payload);
        }
        catch (JsonException)
        {
            return payload;
        }

        if (node is null)
        {
            return payload;
        }

        RedactNode(node);
        return node.ToJsonString(JsonOptions);
    }

    private static void RedactNode(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            var keys = obj.Select(x => x.Key).ToList();
            foreach (var key in keys)
            {
                if (SensitiveFieldNames.Contains(key))
                {
                    obj[key] = "[REDACTED]";
                    continue;
                }

                var child = obj[key];
                if (child is not null)
                {
                    RedactNode(child);
                }
            }

            return;
        }

        if (node is JsonArray array)
        {
            foreach (var item in array)
            {
                if (item is not null)
                {
                    RedactNode(item);
                }
            }
        }
    }
}
