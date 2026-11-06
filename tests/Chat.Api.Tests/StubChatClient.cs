using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace Chat.Api.Tests;

/// <summary>
/// A chat client that answers with a fixed text and remembers what it was asked. Replacing the model
/// with it is what makes the endpoint tests run offline and deterministically: the agent's prompt,
/// its tool surface and the conversation history still travel through the real stack, only the model
/// itself is not real.
/// </summary>
/// <param name="answer">Text to answer every request with.</param>
public sealed class StubChatClient(string answer) : IChatClient
{
    private readonly Lock _gate = new();
    private readonly List<IReadOnlyList<ChatMessage>> _requests = [];

    /// <summary>The messages of every request this client received, oldest first.</summary>
    public IReadOnlyList<IReadOnlyList<ChatMessage>> Requests
    {
        get
        {
            lock (_gate)
            {
                return [.. _requests];
            }
        }
    }

    /// <inheritdoc />
    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        Record(messages);

        return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, answer)));
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Record(messages);

        // Word by word, so the caller genuinely receives several updates rather than one.
        foreach (var word in answer.Split(' '))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return new ChatResponseUpdate(ChatRole.Assistant, $"{word} ");
        }

        await Task.CompletedTask;
    }

    /// <inheritdoc />
    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    /// <inheritdoc />
    public void Dispose()
    {
    }

    private void Record(IEnumerable<ChatMessage> messages)
    {
        lock (_gate)
        {
            _requests.Add([.. messages]);
        }
    }
}
