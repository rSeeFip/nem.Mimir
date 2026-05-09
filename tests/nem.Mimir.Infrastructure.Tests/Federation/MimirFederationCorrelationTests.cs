namespace nem.Mimir.Infrastructure.Tests.Federation;

using NSubstitute;
using Shouldly;
using nem.Contracts.AspNetCore.Messaging.Federation;
using Wolverine;

public sealed class MimirFederationCorrelationTests
{
    [Fact]
    public async Task FederationCorrelation_PropagatesCorrelationAndTraceparentHeaders()
    {
        var middleware = new FederationCorrelationMiddleware();
        var accessor = new FederationCorrelationContextAccessor();
        var context = Substitute.For<IMessageContext>();
        var incoming = new Envelope(new object())
        {
            CorrelationId = "corr-inbound",
        };
        incoming.Headers[FederationCorrelationHeaderNames.CorrelationId] = "corr-inbound";
        incoming.Headers[FederationCorrelationHeaderNames.TraceParent] = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-00";
        incoming.Headers[FederationCorrelationHeaderNames.SourceTenantId] = "org-a::mimir-a";
        incoming.Headers[FederationCorrelationHeaderNames.TargetTenantId] = "org-b::mimir-b";
        context.Envelope.Returns(incoming);

        await middleware.BeforeAsync(context, accessor);

        var outgoing = new Envelope(new object());
        FederationCorrelationMiddleware.ApplyOutgoing(outgoing);

        await middleware.FinallyAsync(accessor);

        outgoing.CorrelationId.ShouldBe("corr-inbound");
        outgoing.Headers[FederationCorrelationHeaderNames.CorrelationId].ShouldBe("corr-inbound");
        outgoing.Headers[FederationCorrelationHeaderNames.TraceParent].ShouldBe("00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-00");
        outgoing.Headers[FederationCorrelationHeaderNames.SourceTenantId].ShouldBe("org-a::mimir-a");
        outgoing.Headers[FederationCorrelationHeaderNames.TargetTenantId].ShouldBe("org-b::mimir-b");
    }
}
