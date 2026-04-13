using Marten;
using nem.Mimir.Application.Notes.Services;
using NSubstitute;
using Shouldly;

namespace nem.Mimir.Application.Tests.Notes.Services;

public sealed class AutomergeDocumentStoreTests
{
    [Fact]
    public async Task SaveState_ThenLoadState_RoundTripsDocumentState()
    {
        var session = Substitute.For<Marten.IDocumentSession>();
        AutomergeDocumentStoreDocument? stored = null;

        session.LoadAsync<AutomergeDocumentStoreDocument>("automerge:doc-1", Arg.Any<CancellationToken>())
            .Returns(_ => stored);
        session.When(x => x.Store(Arg.Any<object>()))
            .Do(call => stored = (AutomergeDocumentStoreDocument)call.Args()[0]);

        var store = new AutomergeDocumentStore(session, TimeProvider.System);
        var initialState = new byte[] { 0x01, 0x02, 0x03 };

        await store.SaveState("doc-1", initialState, TestContext.Current.CancellationToken);
        var loadedState = await store.LoadState("doc-1", TestContext.Current.CancellationToken);

        loadedState.SequenceEqual(initialState).ShouldBeTrue();
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Synchronize_TwoClients_ConvergeOnSameState()
    {
        using var left = new AutomergeServerDocument();
        using var right = new AutomergeServerDocument();

        var leftBaseline = left.Save();
        var rightBaseline = right.Save();

        left.ReceiveSyncMessage(new AutomergeSyncSession(), new byte[] { 0x11, 0x22 });
        right.ReceiveSyncMessage(new AutomergeSyncSession(), new byte[] { 0x33, 0x44 });

        AutomergeDocumentStore.Synchronize(left, right);

        left.Save().SequenceEqual(right.Save()).ShouldBeTrue();
        left.Save().SequenceEqual(leftBaseline).ShouldBeFalse();
        right.Save().SequenceEqual(rightBaseline).ShouldBeFalse();
    }

    [Fact]
    public async Task ApplyChanges_WithLegacyYjsBytes_PersistsLegacyPayloadForCompatibility()
    {
        var session = Substitute.For<Marten.IDocumentSession>();
        AutomergeDocumentStoreDocument? stored = new()
        {
            Id = "automerge:doc-legacy",
            DocumentId = "doc-legacy",
            State = new byte[] { 0x01 },
        };

        session.LoadAsync<AutomergeDocumentStoreDocument>("automerge:doc-legacy", Arg.Any<CancellationToken>())
            .Returns(_ => stored);
        session.When(x => x.Store(Arg.Any<object>()))
            .Do(call => stored = (AutomergeDocumentStoreDocument)call.Args()[0]);

        var store = new AutomergeDocumentStore(session, TimeProvider.System);
        var legacyPayload = new byte[] { 0xFE, 0xED, 0xFA, 0xCE };

        var merged = await store.ApplyChanges("doc-legacy", legacyPayload, TestContext.Current.CancellationToken);

        merged.SequenceEqual(legacyPayload).ShouldBeTrue();
        (await store.LoadState("doc-legacy", TestContext.Current.CancellationToken)).SequenceEqual(legacyPayload).ShouldBeTrue();
    }
}
