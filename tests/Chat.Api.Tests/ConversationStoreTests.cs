using Chat.Api.Chat;
using Microsoft.Extensions.AI;

namespace Chat.Api.Tests;

/// <summary>
/// Tests of the in-memory conversation history: what a conversation remembers, what it forgets, and
/// that two conversations never see each other.
/// </summary>
public sealed class ConversationStoreTests
{
    [Fact]
    public void GetHistory_IsEmptyForAConversationThatNeverSpoke()
    {
        var store = new ConversationStore(maxTurns: 5);

        Assert.Empty(store.GetHistory("unknown"));
    }

    [Fact]
    public void GetHistory_ReturnsTheTurnsOldestFirst()
    {
        var store = new ConversationStore(maxTurns: 5);

        store.AppendTurn("one", "first question", "first answer");
        store.AppendTurn("one", "second question", "second answer");

        Assert.Collection(
            store.GetHistory("one"),
            message => AssertMessage(message, ChatRole.User, "first question"),
            message => AssertMessage(message, ChatRole.Assistant, "first answer"),
            message => AssertMessage(message, ChatRole.User, "second question"),
            message => AssertMessage(message, ChatRole.Assistant, "second answer"));
    }

    [Fact]
    public void GetHistory_KeepsConversationsApart()
    {
        var store = new ConversationStore(maxTurns: 5);

        store.AppendTurn("left", "left question", "left answer");
        store.AppendTurn("right", "right question", "right answer");

        // One turn is a user message and an assistant message, so each conversation holds two.
        var left = store.GetHistory("left");
        var right = store.GetHistory("right");

        Assert.Equal(2, left.Count);
        Assert.Equal(2, right.Count);
        Assert.Equal("left question", left[0].Text);
        Assert.Equal("right question", right[0].Text);
    }

    [Fact]
    public void GetHistory_ReturnsASnapshotTheStoreCannotChangeUnderneathTheCaller()
    {
        var store = new ConversationStore(maxTurns: 5);

        store.AppendTurn("one", "first question", "first answer");
        var snapshot = store.GetHistory("one");

        store.AppendTurn("one", "second question", "second answer");

        // The caller sends its snapshot to the agent while other requests keep writing to the store:
        // the snapshot stays at the pair it was taken with, not the four messages now stored.
        Assert.Equal(2, snapshot.Count);
        Assert.Equal("first question", snapshot[0].Text);
        Assert.Equal("first answer", snapshot[1].Text);
    }

    [Fact]
    public void AppendTurn_DropsTheOldestTurnsBeyondTheCap()
    {
        var store = new ConversationStore(maxTurns: 2);

        store.AppendTurn("one", "first question", "first answer");
        store.AppendTurn("one", "second question", "second answer");
        store.AppendTurn("one", "third question", "third answer");

        var history = store.GetHistory("one");

        Assert.Equal(4, history.Count);
        Assert.Equal("second question", history[0].Text);
        Assert.Equal("third answer", history[^1].Text);
    }

    private static void AssertMessage(ChatMessage message, ChatRole role, string text)
    {
        Assert.Equal(role, message.Role);
        Assert.Equal(text, message.Text);
    }
}
