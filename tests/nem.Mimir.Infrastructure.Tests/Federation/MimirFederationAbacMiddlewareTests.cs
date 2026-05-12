namespace nem.Mimir.Infrastructure.Tests.Federation;

using System.Text.Json;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using nem.Contracts.Federation.Authorization;
using nem.Contracts.Identity;
using nem.Mimir.Api.Federation;
using Wolverine;

public sealed class MimirFederationAbacMiddlewareTests
{
    private readonly ILogger<MimirFederationAbacMiddleware> _logger = Substitute.For<ILogger<MimirFederationAbacMiddleware>>();
    private readonly MimirFederationAbacMiddleware _sut;

    public MimirFederationAbacMiddlewareTests()
    {
        _sut = new MimirFederationAbacMiddleware(_logger);
    }

    [Fact]
    public void FederationAbac_Before_GracefullyDegrades_WhenAuthorizationContextHeaderMissing()
    {
        var accessor = new MimirFederationAbacContextAccessor();
        var envelope = new Envelope(new object());
        envelope.Headers["traceparent"] = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-00";

        _sut.Before(envelope, accessor);

        accessor.Current.ShouldBeNull();
    }

    [Fact]
    public void FederationAbac_Before_CapturesAuthorizationContext_WhenHeaderPresent()
    {
        var accessor = new MimirFederationAbacContextAccessor();
        var envelope = new Envelope(new object());
        envelope.Headers["traceparent"] = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-00";

        var expected = new ClassificationAuthorizationContext(
            Action: "read",
            User: new UserAttributes(
                UserId: "user-1",
                Roles: [FederationRole.Consumer],
                ClearanceLevel: UserClearanceLevel.Confidential,
                SegmentScopes: [SegmentId.New()]),
            Resource: new ResourceAttributes(
                Service: "nem.Mimir",
                EntityType: "conversation",
                EntityId: Guid.NewGuid().ToString("N"),
                ClassificationLevel: ResourceClassificationLevel.Confidential,
                HasPii: false),
            Segment: new SegmentAttributes(
                SourceSegmentId: SegmentId.New(),
                TargetSegmentId: SegmentId.New(),
                IsCrossSegment: true));

        envelope.Headers[MimirFederationAbacMiddleware.AuthorizationContextHeaderName] = JsonSerializer.Serialize(expected);

        _sut.Before(envelope, accessor);

        accessor.Current.ShouldNotBeNull();
        accessor.Current!.Action.ShouldBe(expected.Action);
        accessor.Current.User.UserId.ShouldBe(expected.User.UserId);
        accessor.Current.User.ClearanceLevel.ShouldBe(expected.User.ClearanceLevel);
        accessor.Current.User.Roles.ShouldBe(expected.User.Roles, ignoreOrder: false);
        accessor.Current.User.SegmentScopes.ShouldBe(expected.User.SegmentScopes, ignoreOrder: false);
        accessor.Current.Resource.Service.ShouldBe(expected.Resource.Service);
        accessor.Current.Resource.EntityType.ShouldBe(expected.Resource.EntityType);
        accessor.Current.Resource.EntityId.ShouldBe(expected.Resource.EntityId);
        accessor.Current.Resource.ClassificationLevel.ShouldBe(expected.Resource.ClassificationLevel);
        accessor.Current.Resource.HasPii.ShouldBe(expected.Resource.HasPii);
        accessor.Current.Segment.SourceSegmentId.ShouldBe(expected.Segment.SourceSegmentId);
        accessor.Current.Segment.TargetSegmentId.ShouldBe(expected.Segment.TargetSegmentId);
        accessor.Current.Segment.IsCrossSegment.ShouldBe(expected.Segment.IsCrossSegment);
        _sut.After(accessor);
        accessor.Current.ShouldBeNull();
    }
}
