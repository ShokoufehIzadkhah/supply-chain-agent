using System.Text;
using Agent.Core;
using Agent.Tools;
using Agent.Tools.Rag;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Chat.Api.Chat;

/// <summary>
/// The chat endpoints. Every request builds its own agent on the shared chat client, with tools that
/// report back to that request, so the calls in a response belong to the answer that made them.
/// </summary>
public static class ChatEndpoints
{
    /// <summary>Maps the <c>/chat</c> endpoints.</summary>
    /// <param name="app">Endpoint route builder.</param>
    /// <returns>The same builder, so calls can be chained.</returns>
    public static IEndpointRouteBuilder MapChatEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/chat").WithTags("Chat");

        group.MapPost("", ChatAsync)
            .WithName("Chat")
            .WithSummary("Answers a question, with the tool calls it made and the policy sources it used.");

        group.MapPost("/stream", ChatStreamAsync)
            .WithName("ChatStream")
            .WithSummary("Answers a question as server-sent events: delta, tool and done.");

        return app;
    }

    /// <summary>Answers one question and reports what the agent did to produce the answer.</summary>
    private static async Task<IResult> ChatAsync(
        ChatRequest request,
        ConversationStore store,
        IChatClient chatClient,
        OrderTools orderTools,
        PolicyTools policyTools,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        if (Validate(request) is { } invalid)
        {
            return invalid;
        }

        var collector = new ToolCallCollector();
        var agent = CreateAgent(chatClient, orderTools, policyTools, collector, loggerFactory);

        var response = await agent.RunAsync(
            BuildMessages(store.GetHistory(request.ConversationId), request.Message),
            session: null,
            cancellationToken: cancellationToken);

        store.AppendTurn(request.ConversationId, request.Message, response.Text);

        return Results.Ok(new ChatResponse(response.Text, collector.ToToolCalls(), collector.ToSources()));
    }

    /// <summary>
    /// Answers one question as server-sent events, so the UI can show the answer as it is written and
    /// each tool call as it finishes. The <c>done</c> event repeats the tool calls and the sources the
    /// <c>/chat</c> endpoint returns in the response body.
    /// </summary>
    private static async Task ChatStreamAsync(
        ChatRequest request,
        HttpContext context,
        ConversationStore store,
        IChatClient chatClient,
        OrderTools orderTools,
        PolicyTools policyTools,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        if (Validate(request) is { } invalid)
        {
            await invalid.ExecuteAsync(context);
            return;
        }

        var events = new ServerSentEvents(context.Response);
        var answer = new StringBuilder();
        var collector = new ToolCallCollector((call, token) => events.WriteAsync("tool", call, token));
        var agent = CreateAgent(chatClient, orderTools, policyTools, collector, loggerFactory);

        try
        {
            await foreach (var update in agent.RunStreamingAsync(
                BuildMessages(store.GetHistory(request.ConversationId), request.Message),
                session: null,
                cancellationToken: cancellationToken))
            {
                if (!string.IsNullOrEmpty(update.Text))
                {
                    answer.Append(update.Text);
                    await events.WriteAsync("delta", new { text = update.Text }, cancellationToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // The client went away; there is no one left to answer.
            return;
        }
        catch (Exception ex)
        {
            loggerFactory
                .CreateLogger(nameof(ChatEndpoints))
                .LogError(ex, "Answering conversation {ConversationId} failed.", request.ConversationId);

            await events.WriteAsync(
                "error",
                new { message = "The assistant could not answer that question." },
                CancellationToken.None);

            return;
        }

        store.AppendTurn(request.ConversationId, request.Message, answer.ToString());

        await events.WriteAsync(
            "done",
            new ChatResponse(answer.ToString(), collector.ToToolCalls(), collector.ToSources()),
            CancellationToken.None);
    }

    private static AIAgent CreateAgent(
        IChatClient chatClient,
        OrderTools orderTools,
        PolicyTools policyTools,
        ToolCallCollector collector,
        ILoggerFactory loggerFactory)
        => OrderAssistant.Create(
            chatClient,
            AgentToolset
                .ToFunctions(orderTools, policyTools)
                .Select(function => new ToolCallRecorder(function, collector.AddAsync)),
            loggerFactory);

    /// <summary>
    /// The messages to send: the turns this conversation already exchanged, then the new question. The
    /// system prompt is not here — it travels with the agent as its instructions.
    /// </summary>
    private static List<ChatMessage> BuildMessages(IReadOnlyList<ChatMessage> history, string message)
    {
        List<ChatMessage> messages = [.. history];
        messages.Add(new ChatMessage(ChatRole.User, message));
        return messages;
    }

    private static IResult? Validate(ChatRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.ConversationId))
        {
            errors["conversationId"] = ["conversationId is required."];
        }

        if (string.IsNullOrWhiteSpace(request.Message))
        {
            errors["message"] = ["message is required."];
        }

        return errors.Count == 0 ? null : Results.ValidationProblem(errors);
    }
}
