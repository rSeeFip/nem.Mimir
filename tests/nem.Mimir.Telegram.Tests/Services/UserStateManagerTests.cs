using nem.Mimir.Telegram.Services;
using Shouldly;

namespace nem.Mimir.Telegram.Tests.Services;

public sealed class UserStateManagerTests
{
    private readonly UserStateManager _sut = new();

    [Fact]
    public void GetState_ReturnsNull_WhenUserHasNoState()
    {
        var result = _sut.GetState(12345);

        result.ShouldBeNull();
    }

    [Fact]
    public void GetOrCreateState_CreatesNewState_WhenUserHasNoState()
    {
        var state = _sut.GetOrCreateState(12345);

        state.ShouldNotBeNull();
        state.IsAuthenticated.ShouldBeFalse();
        state.BearerToken.ShouldBeNull();
        state.CurrentConversationId.ShouldBeNull();
        state.SelectedModel.ShouldBeNull();
    }

    [Fact]
    public void GetOrCreateState_ReturnsSameState_OnSubsequentCalls()
    {
        var state1 = _sut.GetOrCreateState(12345);
        var state2 = _sut.GetOrCreateState(12345);

        state1.ShouldBeSameAs(state2);
    }

    [Fact]
    public void SetAuthenticated_SetsTokenAndFlag()
    {
        _sut.SetAuthenticated(12345, "test-token");

        var state = _sut.GetState(12345);
        state.ShouldNotBeNull();
        state.IsAuthenticated.ShouldBeTrue();
        state.BearerToken.ShouldBe("test-token");
    }

    [Fact]
    public void IsAuthenticated_ReturnsFalse_WhenUserHasNoState()
    {
        _sut.IsAuthenticated(99999).ShouldBeFalse();
    }

    [Fact]
    public void IsAuthenticated_ReturnsTrue_AfterSetAuthenticated()
    {
        _sut.SetAuthenticated(12345, "test-token");

        _sut.IsAuthenticated(12345).ShouldBeTrue();
    }

    [Fact]
    public void SetCurrentConversation_SetsConversationIdAndTitle()
    {
        var conversationId = Guid.NewGuid();

        _sut.SetCurrentConversation(12345, conversationId, "Test Chat");

        var state = _sut.GetState(12345);
        state.ShouldNotBeNull();
        state.CurrentConversationId.ShouldBe(conversationId);
        state.CurrentConversationTitle.ShouldBe("Test Chat");
    }

    [Fact]
    public void SetSelectedModel_SetsModel()
    {
        _sut.SetSelectedModel(12345, "gpt-4");

        var state = _sut.GetState(12345);
        state.ShouldNotBeNull();
        state.SelectedModel.ShouldBe("gpt-4");
    }

    [Fact]
    public void DifferentUsers_HaveIndependentState()
    {
        _sut.SetAuthenticated(111, "token-a");
        _sut.SetSelectedModel(222, "gpt-4");

        _sut.IsAuthenticated(111).ShouldBeTrue();
        _sut.IsAuthenticated(222).ShouldBeFalse();

        var state111 = _sut.GetState(111);
        var state222 = _sut.GetState(222);

        state111!.SelectedModel.ShouldBeNull();
        state222!.SelectedModel.ShouldBe("gpt-4");
    }
}
