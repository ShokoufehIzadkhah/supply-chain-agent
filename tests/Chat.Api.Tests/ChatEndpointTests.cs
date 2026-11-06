using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.AI;
using ChatResponse = Chat.Api.Chat.ChatResponse;

namespace Chat.Api.Tests;

/// <summary>
/// End-to-end tests of the chat endpoints. The model is a stub, so what is being tested is the wiring:
/// the request and response shapes, the validation, and the conversation history the API keeps.
/// </summary>
public sealed class ChatEndpointTests(ChatApiFactory factory) : IClassFixture<ChatApiFactory>
{
    [Fact]
    public async Task Chat_AnswersInTheShapeTheUiExpects()
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/chat",
            new { conversationId = "shape", message = "When does order 4711 leave the plant?" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var chat = await response.Content.ReadFromJsonAsync<ChatResponse>();

        Assert.NotNull(chat);
        Assert.Equal(ChatApiFactory.AnswerText, chat.Answer);
        Assert.Empty(chat.ToolCalls);
        Assert.Empty(chat.Sources);
    }

    [Fact]
    public async Task Chat_RejectsARequestWithoutAConversationOrAMessage()
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/chat", new { conversationId = "", message = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();

        Assert.Contains("conversationId", body);
        Assert.Contains("message", body);
    }

    [Fact]
    public async Task Chat_SendsTheEarlierTurnsOfTheSameConversation()
    {
        using var client = factory.CreateClient();
        var before = factory.ChatClient.Requests.Count;

        await client.PostAsJsonAsync("/chat", new { conversationId = "history", message = "First question" });
        await client.PostAsJsonAsync("/chat", new { conversationId = "history", message = "Second question" });

        var requests = factory.ChatClient.Requests.Skip(before).ToList();
        var last = requests[^1];

        // The second request carries the first turn, so follow-up questions like "and for that order?"
        // can be understood.
        Assert.Contains(last, message => message.Role == ChatRole.User && message.Text == "First question");
        Assert.Contains(last, message => message.Role == ChatRole.Assistant && message.Text == ChatApiFactory.AnswerText);
        Assert.Contains(last, message => message.Role == ChatRole.User && message.Text == "Second question");
    }

    [Fact]
    public async Task Chat_KeepsConversationsApart()
    {
        using var client = factory.CreateClient();
        var before = factory.ChatClient.Requests.Count;

        await client.PostAsJsonAsync("/chat", new { conversationId = "left", message = "Only mine" });
        await client.PostAsJsonAsync("/chat", new { conversationId = "right", message = "Only yours" });

        var requests = factory.ChatClient.Requests.Skip(before).ToList();
        var last = requests[^1];

        Assert.DoesNotContain(last, message => message.Text == "Only mine");
    }

    [Fact]
    public async Task ChatStream_WritesTheAnswerAsEventsAndEndsWithDone()
    {
        using var client = factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/chat/stream")
        {
            Content = JsonContent.Create(new { conversationId = "stream", message = "Answer me slowly" }),
        };

        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadAsStringAsync();

        Assert.Contains("event: delta", body);
        Assert.Contains("event: done", body);

        // The done event repeats the tool calls and the sources in the same shape /chat returns.
        Assert.Contains("\"answer\":\"", body);
        Assert.Contains("\"toolCalls\":[]", body);
        Assert.Contains("\"sources\":[]", body);
    }

    [Fact]
    public async Task ChatStream_RejectsARequestWithoutAMessage()
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/chat/stream",
            new { conversationId = "stream-invalid", message = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
