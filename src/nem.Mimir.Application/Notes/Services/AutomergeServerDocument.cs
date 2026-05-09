using System.Runtime.InteropServices;

namespace nem.Mimir.Application.Notes.Services;

internal sealed class AutomergeServerDocument : IDisposable
{
    private static readonly AutomergeBridge Bridge = AutomergeBridgeFactory.Create();

    private nint _handle;

    public AutomergeServerDocument()
    {
        _handle = Bridge.CreateDocument();
    }

    private AutomergeServerDocument(nint handle)
    {
        _handle = handle;
    }

    public static AutomergeServerDocument Load(byte[] state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return new AutomergeServerDocument(Bridge.Load(state));
    }

    public byte[] Save()
    {
        return Bridge.Save(RequireHandle());
    }

    public byte[]? GenerateSyncMessage(AutomergeSyncSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        var message = Bridge.GenerateSyncMessage(RequireHandle(), session.RemotePeerState);
        if (message is not null)
        {
            session.LocalPeerState = Save();
        }

        return message;
    }

    public void ReceiveSyncMessage(AutomergeSyncSession session, byte[] syncMessage)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(syncMessage);

        session.RemotePeerState = Bridge.ReceiveSyncMessage(RequireHandle(), session.LocalPeerState, syncMessage);
    }

    public void Dispose()
    {
        var handle = Interlocked.Exchange(ref _handle, nint.Zero);
        if (handle != nint.Zero)
        {
            Bridge.Free(handle);
        }
    }

    private nint RequireHandle()
    {
        ObjectDisposedException.ThrowIf(_handle == nint.Zero, this);
        return _handle;
    }
}
