using nem.Mimir.Api.Initialization;
using nem.Contracts.Initialization;
using Shouldly;

namespace nem.Mimir.Api.Tests.Initialization;

public sealed class MimirInitManifestProviderTests
{
    private readonly MimirInitManifestProvider _sut = new();

    [Fact]
    public async Task GetManifestFragmentAsync_ReturnsCorrectServiceName()
    {
        var fragment = await _sut.GetManifestFragmentAsync();

        fragment.ServiceName.ShouldBe("nem.Mimir");
    }

    [Fact]
    public async Task GetManifestFragmentAsync_IncludesPostgreSQLWithPgvectorRequirement()
    {
        var fragment = await _sut.GetManifestFragmentAsync();

        fragment.DatabaseRequirements.ShouldHaveSingleItem();
        var db = fragment.DatabaseRequirements[0];
        db.Type.ShouldBe("PostgreSQL");
        db.Extensions.ShouldContain("vector");
    }

    [Fact]
    public async Task GetManifestFragmentAsync_IncludesRequiredPostgreSQLConnectionString()
    {
        var fragment = await _sut.GetManifestFragmentAsync();

        var item = fragment.RequiredConfigItems.Single(i => i.Key == "ConnectionStrings:PostgreSQL");
        item.Required.ShouldBeTrue();
        item.IsSecret.ShouldBeFalse();
        item.Category.ShouldBe("required");
    }

    [Fact]
    public async Task GetManifestFragmentAsync_IncludesRequiredRabbitMQHost()
    {
        var fragment = await _sut.GetManifestFragmentAsync();

        var item = fragment.RequiredConfigItems.Single(i => i.Key == "RabbitMQ:Host");
        item.Required.ShouldBeTrue();
        item.IsSecret.ShouldBeFalse();
    }

    [Fact]
    public async Task GetManifestFragmentAsync_IncludesLlmEndpointAndApiKey()
    {
        var fragment = await _sut.GetManifestFragmentAsync();

        var endpoint = fragment.RequiredConfigItems.Single(i => i.Key == "LLM:Endpoint");
        endpoint.Required.ShouldBeTrue();
        endpoint.IsSecret.ShouldBeFalse();

        var apiKey = fragment.RequiredConfigItems.Single(i => i.Key == "LLM:ApiKey");
        apiKey.Required.ShouldBeTrue();
        apiKey.IsSecret.ShouldBeTrue();
    }

    [Fact]
    public async Task GetManifestFragmentAsync_LlmApiKey_IsMarkedAsSecret()
    {
        var fragment = await _sut.GetManifestFragmentAsync();

        var apiKey = fragment.RequiredConfigItems.Single(i => i.Key == "LLM:ApiKey");
        apiKey.IsSecret.ShouldBeTrue();
    }

    [Fact]
    public async Task GetManifestFragmentAsync_IncludesKeycloakAuthority()
    {
        var fragment = await _sut.GetManifestFragmentAsync();

        var item = fragment.RequiredConfigItems.Single(i => i.Key == "Keycloak:Authority");
        item.Required.ShouldBeTrue();
        item.IsSecret.ShouldBeFalse();
    }

    [Fact]
    public async Task GetManifestFragmentAsync_IncludesMcpServerEndpoint()
    {
        var fragment = await _sut.GetManifestFragmentAsync();

        var item = fragment.RequiredConfigItems.Single(i => i.Key == "MCP:ServerEndpoint");
        item.Required.ShouldBeTrue();
        item.Category.ShouldBe("required");
    }

    [Fact]
    public async Task GetManifestFragmentAsync_IncludesOpenSandboxEndpointAsBestPractice()
    {
        var fragment = await _sut.GetManifestFragmentAsync();

        var item = fragment.RequiredConfigItems.Single(i => i.Key == "OpenSandbox:Endpoint");
        item.Required.ShouldBeFalse();
        item.Category.ShouldBe("best-practice");
    }

    [Fact]
    public async Task GetManifestFragmentAsync_IncludesTeamsChannelAdapter()
    {
        var fragment = await _sut.GetManifestFragmentAsync();

        var item = fragment.RequiredConfigItems.Single(i => i.Key == "Channels:Teams:WebhookUrl");
        item.Required.ShouldBeFalse();
        item.Category.ShouldBe("best-practice");
        item.IsSecret.ShouldBeFalse();
    }

    [Fact]
    public async Task GetManifestFragmentAsync_WhatsAppApiKey_IsMarkedAsSecret()
    {
        var fragment = await _sut.GetManifestFragmentAsync();

        var item = fragment.RequiredConfigItems.Single(i => i.Key == "Channels:WhatsApp:ApiKey");
        item.IsSecret.ShouldBeTrue();
        item.Category.ShouldBe("best-practice");
    }

    [Fact]
    public async Task GetManifestFragmentAsync_IncludesSignalChannelAdapter()
    {
        var fragment = await _sut.GetManifestFragmentAsync();

        var item = fragment.RequiredConfigItems.Single(i => i.Key == "Channels:Signal:Endpoint");
        item.Required.ShouldBeFalse();
        item.Category.ShouldBe("best-practice");
    }

    [Fact]
    public async Task GetManifestFragmentAsync_SecretReferences_ContainsLlmApiKeyRef()
    {
        var fragment = await _sut.GetManifestFragmentAsync();

        fragment.SecretReferences.ShouldContain("mimir/llm-api-key");
    }

    [Fact]
    public async Task GetManifestFragmentAsync_SecretReferences_DoesNotContainSecretValues()
    {
        var fragment = await _sut.GetManifestFragmentAsync();

        foreach (var secretRef in fragment.SecretReferences)
        {
            secretRef.ShouldNotContain("=");
            secretRef.ShouldNotContain("password");
        }
    }

    [Fact]
    public async Task GetManifestFragmentAsync_ConfigItems_SecretsHaveNoDefaultValue()
    {
        var fragment = await _sut.GetManifestFragmentAsync();

        var secretItems = fragment.RequiredConfigItems.Where(i => i.IsSecret);
        foreach (var item in secretItems)
        {
            item.DefaultValue.ShouldBeNull();
        }
    }

    [Fact]
    public async Task GetManifestFragmentAsync_IncludesMessageBrokerTopics()
    {
        var fragment = await _sut.GetManifestFragmentAsync();

        fragment.MessageBrokerTopics.ShouldNotBeEmpty();
        fragment.MessageBrokerTopics.ShouldContain(t => t.StartsWith("nem.mimir."));
    }

    [Fact]
    public async Task GetSchemaAsync_ReturnsValidJson()
    {
        var schema = await _sut.GetSchemaAsync();

        schema.ShouldNotBeNullOrWhiteSpace();
        Should.NotThrow(() => System.Text.Json.JsonDocument.Parse(schema));
    }

    [Fact]
    public async Task GetSchemaAsync_ContainsServiceId()
    {
        var schema = await _sut.GetSchemaAsync();

        schema.ShouldContain("nem.Mimir");
    }

    [Fact]
    public async Task ValidateConfigAsync_WithAllRequiredKeys_ReturnsValid()
    {
        var config = new Dictionary<string, string>
        {
            ["ConnectionStrings:PostgreSQL"] = "Host=localhost;Database=mimir;",
            ["RabbitMQ:Host"] = "localhost",
            ["LLM:Endpoint"] = "http://localhost:4000",
            ["LLM:ApiKey"] = "test-api-key",
            ["Keycloak:Authority"] = "http://localhost:8080/realms/nem",
            ["MCP:ServerEndpoint"] = "http://localhost:5000"
        };

        var result = await _sut.ValidateConfigAsync(config);

        result.IsValid.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
    }

    [Fact]
    public async Task ValidateConfigAsync_MissingPostgreSQL_ReturnsError()
    {
        var config = new Dictionary<string, string>
        {
            ["RabbitMQ:Host"] = "localhost",
            ["LLM:Endpoint"] = "http://localhost:4000",
            ["LLM:ApiKey"] = "test-api-key",
            ["Keycloak:Authority"] = "http://localhost:8080/realms/nem",
            ["MCP:ServerEndpoint"] = "http://localhost:5000"
        };

        var result = await _sut.ValidateConfigAsync(config);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.Key == "ConnectionStrings:PostgreSQL");
    }

    [Fact]
    public async Task ValidateConfigAsync_MissingLlmEndpoint_ReturnsError()
    {
        var config = new Dictionary<string, string>
        {
            ["ConnectionStrings:PostgreSQL"] = "Host=localhost;Database=mimir;",
            ["RabbitMQ:Host"] = "localhost",
            ["LLM:ApiKey"] = "test-api-key",
            ["Keycloak:Authority"] = "http://localhost:8080/realms/nem",
            ["MCP:ServerEndpoint"] = "http://localhost:5000"
        };

        var result = await _sut.ValidateConfigAsync(config);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.Key == "LLM:Endpoint");
    }

    [Fact]
    public async Task ValidateConfigAsync_InvalidUri_ForLlmEndpoint_ReturnsError()
    {
        var config = new Dictionary<string, string>
        {
            ["ConnectionStrings:PostgreSQL"] = "Host=localhost;Database=mimir;",
            ["RabbitMQ:Host"] = "localhost",
            ["LLM:Endpoint"] = "not-a-valid-uri",
            ["LLM:ApiKey"] = "test-api-key",
            ["Keycloak:Authority"] = "http://localhost:8080/realms/nem",
            ["MCP:ServerEndpoint"] = "http://localhost:5000"
        };

        var result = await _sut.ValidateConfigAsync(config);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.Key == "LLM:Endpoint");
    }

    [Fact]
    public async Task ValidateConfigAsync_EmptyConfig_ReturnsMultipleErrors()
    {
        var result = await _sut.ValidateConfigAsync([]);

        result.IsValid.ShouldBeFalse();
        result.Errors.Count.ShouldBeGreaterThanOrEqualTo(6);
    }

    [Fact]
    public async Task GetManifestFragmentAsync_ImplementsIInitManifestProvider()
    {
        IInitManifestProvider provider = _sut;

        var fragment = await provider.GetManifestFragmentAsync();
        var schema = await provider.GetSchemaAsync();
        var validation = await provider.ValidateConfigAsync([]);

        fragment.ShouldNotBeNull();
        schema.ShouldNotBeNull();
        validation.ShouldNotBeNull();
    }
}
