using System.Collections.Concurrent;
using nem.Contracts.Organism;

namespace nem.Mimir.Api.Federation;

public sealed class MimirFederationPeerHealthState
{
    private readonly ConcurrentDictionary<string, FederationPeerHealthUpdateEvent> _latestByPeer = new(StringComparer.OrdinalIgnoreCase);

    public int Count => _latestByPeer.Count;

    public void Update(FederationPeerHealthUpdateEvent healthUpdate)
    {
        ArgumentNullException.ThrowIfNull(healthUpdate);
        _latestByPeer[healthUpdate.PeerServiceId] = healthUpdate;
    }

    public bool TryGet(string peerServiceId, out FederationPeerHealthUpdateEvent? healthUpdate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(peerServiceId);
        return _latestByPeer.TryGetValue(peerServiceId, out healthUpdate);
    }

    public IReadOnlyCollection<string> GetTrackedPeerServiceIds()
    {
        return _latestByPeer.Keys
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
