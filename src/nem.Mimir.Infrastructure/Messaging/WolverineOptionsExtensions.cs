using Microsoft.AspNetCore.Http;
using Wolverine;

namespace nem.Mimir.Infrastructure.Messaging;

public static class WolverineOptionsExtensions
{
    public static WolverineOptions AddMimirFederationPropagation(this WolverineOptions opts)
    {
        ArgumentNullException.ThrowIfNull(opts);

        var httpContextAccessor = new HttpContextAccessor();
        var rule = new FederationHeaderPropagation(httpContextAccessor);

        opts.Policies.AllSenders(subscriber =>
        {
            subscriber.CustomizeOutgoing(rule.Modify);
        });

        return opts;
    }
}
