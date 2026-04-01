using Microsoft.Extensions.Logging.Abstractions;
using nem.Contracts.CloudEvents;
using nem.Contracts.Events;
using nem.Contracts.Events.Integration;
using nem.Contracts.Identity;
using nem.Mimir.Application.Mcp;
using NSubstitute;

namespace nem.Mimir.Application.Tests.Mcp;

public sealed class SkillPublishedEventConsumerTests
{
    [Fact]
    public async Task Handle_invalidates_catalog_cache()
    {
        var catalog = Substitute.For<IMcpToolCatalogService>();
        var payload = new SkillPublishedEvent(SkillId.New(), SkillVersionId.New(), PublisherId.New(), DateTimeOffset.UtcNow);
        var message = NemCloudEvent<SkillPublishedEvent>.Create(EventTypes.Skill.Published, "/nem/skills", payload);

        await SkillPublishedEventConsumer.Handle(message, catalog, NullLoggerFactory.Instance, CancellationToken.None);

        catalog.Received(1).InvalidateCache();
    }
}
