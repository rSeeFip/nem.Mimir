using nem.Contracts.Initialization;
using System.Text.Json;

namespace nem.Mimir.Api.Initialization;

public sealed class MimirInitManifestProvider : IInitManifestProvider
{
    public Task<InitManifestFragment> GetManifestFragmentAsync(CancellationToken ct = default)
    {
        var fragment = new InitManifestFragment
        {
            ServiceName = "nem.Mimir",
            RequiredConfigItems =
            [
                new ManifestConfigItem
                {
                    Key = "ConnectionStrings:PostgreSQL",
                    Type = "ConnectionString",
                    Description = "PostgreSQL with pgvector for conversation embeddings",
                    Required = true,
                    DefaultValue = null,
                    Category = "required",
                    IsSecret = false
                },
                new ManifestConfigItem
                {
                    Key = "RabbitMQ:Host",
                    Type = "String",
                    Description = "RabbitMQ host for messaging",
                    Required = true,
                    DefaultValue = "localhost",
                    Category = "required",
                    IsSecret = false
                },
                new ManifestConfigItem
                {
                    Key = "LLM:Endpoint",
                    Type = "Uri",
                    Description = "LiteLLM or Ollama endpoint for LLM routing",
                    Required = true,
                    DefaultValue = null,
                    Category = "required",
                    IsSecret = false
                },
                new ManifestConfigItem
                {
                    Key = "LLM:ApiKey",
                    Type = "String",
                    Description = "LLM provider API key",
                    Required = true,
                    DefaultValue = null,
                    Category = "required",
                    IsSecret = true
                },
                new ManifestConfigItem
                {
                    Key = "Keycloak:Authority",
                    Type = "Uri",
                    Description = "Keycloak OIDC authority URL for authentication",
                    Required = true,
                    DefaultValue = null,
                    Category = "required",
                    IsSecret = false
                },
                new ManifestConfigItem
                {
                    Key = "MCP:ServerEndpoint",
                    Type = "Uri",
                    Description = "nem.MCP API endpoint",
                    Required = true,
                    DefaultValue = null,
                    Category = "required",
                    IsSecret = false
                },
                new ManifestConfigItem
                {
                    Key = "OpenSandbox:Endpoint",
                    Type = "Uri",
                    Description = "OpenSandbox endpoint for safe code execution",
                    Required = false,
                    DefaultValue = null,
                    Category = "best-practice",
                    IsSecret = false
                },
                new ManifestConfigItem
                {
                    Key = "Channels:Teams:WebhookUrl",
                    Type = "Uri",
                    Description = "Microsoft Teams channel webhook",
                    Required = false,
                    DefaultValue = null,
                    Category = "best-practice",
                    IsSecret = false
                },
                new ManifestConfigItem
                {
                    Key = "Channels:WhatsApp:ApiKey",
                    Type = "String",
                    Description = "WhatsApp Business API key",
                    Required = false,
                    DefaultValue = null,
                    Category = "best-practice",
                    IsSecret = true
                },
                new ManifestConfigItem
                {
                    Key = "Channels:Signal:Endpoint",
                    Type = "Uri",
                    Description = "Signal messaging endpoint",
                    Required = false,
                    DefaultValue = null,
                    Category = "best-practice",
                    IsSecret = false
                }
            ],
            SecretReferences =
            [
                "mimir/llm-api-key",
                "mimir/whatsapp-api-key",
                "mimir/keycloak-client-secret"
            ],
            DatabaseRequirements =
            [
                new DatabaseRequirement
                {
                    Type = "PostgreSQL",
                    Extensions = ["vector"]
                }
            ],
            MessageBrokerTopics =
            [
                "nem.mimir.conversation-started",
                "nem.mimir.message-sent",
                "nem.mimir.message-received",
                "nem.mimir.channel-connected",
                "nem.mimir.channel-disconnected",
                "nem.mimir.llm-response-generated",
                "nem.mimir.agent-tool-invoked"
            ],
            PluginEnablementList = []
        };

        return Task.FromResult(fragment);
    }

    public Task<string> GetSchemaAsync(CancellationToken ct = default)
    {
        var schema = new
        {
            title = "nem.Mimir Configuration Schema",
            version = "1.0.0",
            serviceId = "nem.Mimir",
            required = new[]
            {
                "ConnectionStrings:PostgreSQL",
                "RabbitMQ:Host",
                "LLM:Endpoint",
                "LLM:ApiKey",
                "Keycloak:Authority",
                "MCP:ServerEndpoint"
            },
            properties = new Dictionary<string, object>
            {
                ["ConnectionStrings:PostgreSQL"] = new { type = "string", format = "connection-string", description = "PostgreSQL with pgvector for conversation embeddings" },
                ["RabbitMQ:Host"] = new { type = "string", description = "RabbitMQ host for messaging" },
                ["LLM:Endpoint"] = new { type = "string", format = "uri", description = "LiteLLM or Ollama endpoint for LLM routing" },
                ["LLM:ApiKey"] = new { type = "string", description = "LLM provider API key", secret = true },
                ["Keycloak:Authority"] = new { type = "string", format = "uri", description = "Keycloak OIDC authority URL" },
                ["MCP:ServerEndpoint"] = new { type = "string", format = "uri", description = "nem.MCP API endpoint" },
                ["OpenSandbox:Endpoint"] = new { type = "string", format = "uri", description = "OpenSandbox endpoint for safe code execution" },
                ["Channels:Teams:WebhookUrl"] = new { type = "string", format = "uri", description = "Microsoft Teams channel webhook" },
                ["Channels:WhatsApp:ApiKey"] = new { type = "string", description = "WhatsApp Business API key", secret = true },
                ["Channels:Signal:Endpoint"] = new { type = "string", format = "uri", description = "Signal messaging endpoint" }
            }
        };

        return Task.FromResult(JsonSerializer.Serialize(schema, new JsonSerializerOptions { WriteIndented = true }));
    }

    public Task<ManifestValidationResult> ValidateConfigAsync(
        Dictionary<string, string> config,
        CancellationToken ct = default)
    {
        var errors = new List<ManifestValidationError>();

        var requiredKeys = new[]
        {
            "ConnectionStrings:PostgreSQL",
            "RabbitMQ:Host",
            "LLM:Endpoint",
            "LLM:ApiKey",
            "Keycloak:Authority",
            "MCP:ServerEndpoint"
        };

        foreach (var key in requiredKeys)
        {
            if (!config.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
            {
                errors.Add(new ManifestValidationError
                {
                    Key = key,
                    Message = $"Required configuration key '{key}' is missing or empty."
                });
            }
        }

        var uriKeys = new[]
        {
            "LLM:Endpoint",
            "Keycloak:Authority",
            "MCP:ServerEndpoint",
            "OpenSandbox:Endpoint",
            "Channels:Teams:WebhookUrl",
            "Channels:Signal:Endpoint"
        };

        foreach (var key in uriKeys)
        {
            if (config.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                if (!Uri.TryCreate(value, UriKind.Absolute, out _))
                {
                    errors.Add(new ManifestValidationError
                    {
                        Key = key,
                        Message = $"Configuration key '{key}' must be a valid absolute URI."
                    });
                }
            }
        }

        return Task.FromResult(new ManifestValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors
        });
    }
}
