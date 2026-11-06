using Microsoft.Extensions.AI;

namespace Chat.Api.Chat;

/// <summary>
/// Conversation history, kept in memory for the lifetime of the process: a restarted server starts with
/// no conversations. Each conversation is the user and assistant turns it exchanged; the tool calls an
/// answer made stay inside that turn and are not replayed to the model.
/// </summary>
/// <param name="maxTurns">
/// How many question and answer pairs one conversation keeps. Older turns are dropped, so a conversation
/// that runs all day does not grow without bound.
/// </param>
public sealed class ConversationStore(int maxTurns)
{
    private readonly Dictionary<string, List<ChatMessage>> _conversations = new(StringComparer.Ordinal);
    private readonly Lock _gate = new();

    /// <summary>The turns a conversation has exchanged so far, oldest first.</summary>
    /// <param name="conversationId">Conversation to read.</param>
    /// <returns>A snapshot, so the caller can send it to the agent while the store keeps changing.</returns>
    public IReadOnlyList<ChatMessage> GetHistory(string conversationId)
    {
        lock (_gate)
        {
            return _conversations.TryGetValue(conversationId, out var history) ? [.. history] : [];
        }
    }

    /// <summary>Appends one finished turn and drops the oldest turns beyond the cap.</summary>
    /// <param name="conversationId">Conversation the turn belongs to.</param>
    /// <param name="question">What the user asked.</param>
    /// <param name="answer">What the agent answered.</param>
    public void AppendTurn(string conversationId, string question, string answer)
    {
        lock (_gate)
        {
            if (!_conversations.TryGetValue(conversationId, out var history))
            {
                history = [];
                _conversations[conversationId] = history;
            }

            history.Add(new ChatMessage(ChatRole.User, question));
            history.Add(new ChatMessage(ChatRole.Assistant, answer));

            // Turns are added in pairs, so the excess is always a whole number of turns.
            var excessMessages = history.Count - (maxTurns * 2);

            if (excessMessages > 0)
            {
                history.RemoveRange(0, excessMessages);
            }
        }
    }
}
