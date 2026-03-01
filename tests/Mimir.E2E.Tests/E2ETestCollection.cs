namespace Mimir.E2E.Tests;

/// <summary>
/// Defines a shared test collection so all E2E tests reuse the same
/// <see cref="E2EWebApplicationFactory"/> instance (and thus the same test server,
/// database, message broker, and WireMock server).
/// This prevents Serilog's frozen-logger error when multiple test classes each create their own host.
/// </summary>
[CollectionDefinition(Name)]
public sealed class E2ETestCollection : ICollectionFixture<E2EWebApplicationFactory>
{
    public const string Name = "E2E";
}
