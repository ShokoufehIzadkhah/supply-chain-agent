using System.Text.Json;

namespace Chat.Api.Chat;

/// <summary>
/// Writes server-sent events to a response. Writes are serialized, because a tool call can finish while
/// the answer text is still arriving.
/// </summary>
public sealed class ServerSentEvents
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpResponse _response;
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>Puts the response into event-stream mode.</summary>
    /// <param name="response">Response the events are written to.</param>
    public ServerSentEvents(HttpResponse response)
    {
        _response = response;

        // Headers can only be changed before the first byte is written, so this happens once, here.
        response.ContentType = "text/event-stream";
        response.Headers.CacheControl = "no-cache";
    }

    /// <summary>Writes one named event whose data is <paramref name="payload"/> serialized as JSON.</summary>
    /// <typeparam name="TPayload">Type of the event data.</typeparam>
    /// <param name="eventName">Event name the client listens for.</param>
    /// <param name="payload">Data to serialize into the event.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async ValueTask WriteAsync<TPayload>(
        string eventName,
        TPayload payload,
        CancellationToken cancellationToken = default)
    {
        var data = JsonSerializer.Serialize(payload, Json);

        await _gate.WaitAsync(cancellationToken);

        try
        {
            await _response.WriteAsync($"event: {eventName}\ndata: {data}\n\n", cancellationToken);
            await _response.Body.FlushAsync(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }
}
