using Marten;

namespace nem.Mimir.Application.Notes.Services;

public sealed class AutomergeDocumentStore
{
    private const int MaxSyncIterations = 32;

    private readonly IDocumentSession _documentSession;
    private readonly TimeProvider _timeProvider;

    public AutomergeDocumentStore(IDocumentSession documentSession, TimeProvider timeProvider)
    {
        _documentSession = documentSession;
        _timeProvider = timeProvider;
    }

    public async Task SaveState(string documentId, byte[] state, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentId);
        ArgumentNullException.ThrowIfNull(state);

        var snapshot = await LoadDocumentAsync(documentId, cancellationToken).ConfigureAwait(false);
        snapshot.State = state.ToArray();
        snapshot.UpdatedAt = _timeProvider.GetUtcNow();

        _documentSession.Store(snapshot);
        await _documentSession.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<byte[]> LoadState(string documentId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentId);

        var snapshot = await LoadDocumentAsync(documentId, cancellationToken).ConfigureAwait(false);
        return snapshot.State.ToArray();
    }

    public async Task<byte[]> ApplyChanges(string documentId, byte[] changes, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentId);
        ArgumentNullException.ThrowIfNull(changes);

        if (changes.Length == 0)
        {
            return await LoadState(documentId, cancellationToken).ConfigureAwait(false);
        }

        var currentState = await LoadState(documentId, cancellationToken).ConfigureAwait(false);
        var mergedState = MergeStates(currentState, changes);
        await SaveState(documentId, mergedState, cancellationToken).ConfigureAwait(false);
        return mergedState;
    }

    public async Task<byte[]?> GenerateSyncMessage(string documentId, byte[] peerState, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentId);
        ArgumentNullException.ThrowIfNull(peerState);

        var localState = await LoadState(documentId, cancellationToken).ConfigureAwait(false);
        return TryGenerateSyncMessage(localState, peerState);
    }

    internal static byte[] MergeStates(byte[] leftState, byte[] rightState)
    {
        ArgumentNullException.ThrowIfNull(leftState);
        ArgumentNullException.ThrowIfNull(rightState);

        var leftDocument = leftState.Length == 0 ? new AutomergeServerDocument() : AutomergeServerDocument.Load(leftState);
        var rightDocument = rightState.Length == 0 ? new AutomergeServerDocument() : AutomergeServerDocument.Load(rightState);

        Synchronize(leftDocument, rightDocument);
        return leftDocument.Save();
    }

    internal static byte[]? TryGenerateSyncMessage(byte[] localState, byte[] peerState)
    {
        ArgumentNullException.ThrowIfNull(localState);
        ArgumentNullException.ThrowIfNull(peerState);

        using var localDocument = localState.Length == 0 ? new AutomergeServerDocument() : AutomergeServerDocument.Load(localState);
        var session = new AutomergeSyncSession
        {
            RemotePeerState = peerState.ToArray(),
        };

        return localDocument.GenerateSyncMessage(session);
    }

    internal static void Synchronize(AutomergeServerDocument leftDocument, AutomergeServerDocument rightDocument)
    {
        ArgumentNullException.ThrowIfNull(leftDocument);
        ArgumentNullException.ThrowIfNull(rightDocument);

        var state = new AutomergeSyncSession();

        for (var iteration = 0; iteration < MaxSyncIterations; iteration++)
        {
            var exchanged = false;

            var leftMessage = leftDocument.GenerateSyncMessage(state);
            if (leftMessage is not null)
            {
                exchanged = true;
                rightDocument.ReceiveSyncMessage(state, leftMessage);
            }

            var rightMessage = rightDocument.GenerateSyncMessage(state);
            if (rightMessage is not null)
            {
                exchanged = true;
                leftDocument.ReceiveSyncMessage(state, rightMessage);
            }

            if (!exchanged)
            {
                return;
            }
        }

        throw new InvalidOperationException($"Automerge synchronization did not converge within {MaxSyncIterations} iterations.");
    }

    private async Task<AutomergeDocumentStoreDocument> LoadDocumentAsync(string documentId, CancellationToken cancellationToken)
    {
        var snapshot = await _documentSession.LoadAsync<AutomergeDocumentStoreDocument>(CreateStorageId(documentId), cancellationToken)
            .ConfigureAwait(false);

        return snapshot ?? new AutomergeDocumentStoreDocument
        {
            Id = CreateStorageId(documentId),
            DocumentId = documentId,
            UpdatedAt = _timeProvider.GetUtcNow(),
        };
    }

    private static string CreateStorageId(string documentId) => $"automerge:{documentId}";
}
