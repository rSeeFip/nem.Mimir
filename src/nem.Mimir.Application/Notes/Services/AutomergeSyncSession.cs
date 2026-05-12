namespace nem.Mimir.Application.Notes.Services;

internal sealed class AutomergeSyncSession
{
    public byte[] LocalPeerState { get; set; } = [];

    public byte[] RemotePeerState { get; set; } = [];
}
