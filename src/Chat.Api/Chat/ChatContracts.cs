using System.Text.Json;

namespace Chat.Api.Chat;

/// <summary>One question from the chat UI.</summary>
/// <param name="ConversationId">
/// Identifier the client chooses to keep a conversation's history. Two clients that send the same id
/// share one history.
/// </param>
/// <param name="Message">The user's question.</param>
public sealed record ChatRequest(string ConversationId, string Message);

/// <summary>One tool call the agent made while answering.</summary>
/// <param name="Name">Name of the tool.</param>
/// <param name="Args">Arguments the tool was called with, as JSON.</param>
/// <param name="DurationMs">How long the call took, in milliseconds.</param>
public sealed record ToolCallDto(string Name, JsonElement Args, double DurationMs);

/// <summary>An answer and what the agent did to produce it.</summary>
/// <param name="Answer">The answer text.</param>
/// <param name="ToolCalls">
/// Tool calls made while answering, in the order they finished. They describe the turn rather than the
/// text, so the UI can show what the agent looked up.
/// </param>
/// <param name="Sources">
/// Policy documents the answer is grounded in, as <c>file §section</c>, taken from the policy searches
/// that actually ran.
/// </param>
public sealed record ChatResponse(
    string Answer,
    IReadOnlyList<ToolCallDto> ToolCalls,
    IReadOnlyList<string> Sources);
