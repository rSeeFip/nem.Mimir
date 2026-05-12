namespace nem.Mimir.Api.IntegrationTests;

/// <summary>
/// Defines a shared test collection so all integration tests reuse the same
/// <see cref="MimirWebApplicationFactory"/> instance (and thus the same test server).
/// This prevents Serilog's frozen-logger error when multiple test classes each create their own host.
/// </summary>
[CollectionDefinition(Name)]
public sealed class IntegrationTestCollection : ICollectionFixture<MimirWebApplicationFactory>
{
    public const string Name = "Integration";
}
