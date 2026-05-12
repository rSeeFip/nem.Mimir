namespace nem.Mimir.Application.Notes.Services;

internal interface AutomergeBridge
{
    nint CreateDocument();

    nint Load(byte[] state);

    byte[] Save(nint handle);

    byte[]? GenerateSyncMessage(nint handle, byte[] peerState);

    byte[] ReceiveSyncMessage(nint handle, byte[] peerState, byte[] message);

    void Free(nint handle);
}

internal sealed class ManagedSnapshotAutomergeBridge : AutomergeBridge
{
    private readonly object _syncRoot = new();
    private readonly Dictionary<nint, byte[]> _documents = [];
    private long _nextHandle = 1;

    public nint CreateDocument()
    {
        lock (_syncRoot)
        {
            var handle = new nint(_nextHandle++);
            _documents[handle] = [];
            return handle;
        }
    }

    public nint Load(byte[] state)
    {
        ArgumentNullException.ThrowIfNull(state);
        lock (_syncRoot)
        {
            var handle = new nint(_nextHandle++);
            _documents[handle] = state.ToArray();
            return handle;
        }
    }

    public byte[] Save(nint handle)
    {
        lock (_syncRoot)
        {
            return GetState(handle).ToArray();
        }
    }

    public byte[]? GenerateSyncMessage(nint handle, byte[] peerState)
    {
        ArgumentNullException.ThrowIfNull(peerState);

        lock (_syncRoot)
        {
            var local = GetState(handle);
            return local.AsSpan().SequenceEqual(peerState) ? null : local.ToArray();
        }
    }

    public byte[] ReceiveSyncMessage(nint handle, byte[] peerState, byte[] message)
    {
        ArgumentNullException.ThrowIfNull(peerState);
        ArgumentNullException.ThrowIfNull(message);

        lock (_syncRoot)
        {
            _documents[handle] = message.ToArray();
            return message.ToArray();
        }
    }

    public void Free(nint handle)
    {
        lock (_syncRoot)
        {
            _documents.Remove(handle);
        }
    }

    private byte[] GetState(nint handle)
    {
        if (_documents.TryGetValue(handle, out var state))
        {
            return state;
        }

        throw new InvalidOperationException($"Unknown Automerge document handle: {handle}.");
    }
}

internal static class AutomergeBridgeFactory
{
    public static AutomergeBridge Create()
    {
        return new ManagedSnapshotAutomergeBridge();
    }
}
