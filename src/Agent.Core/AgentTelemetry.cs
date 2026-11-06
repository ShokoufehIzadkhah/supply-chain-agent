using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.AI;

namespace Agent.Core;

/// <summary>
/// The spans and metrics the agent reports. One <see cref="ActivitySource"/> and one
/// <see cref="Meter"/> are shared by every host, so adding a tool or a metric is a change here and
/// nowhere else: a host only subscribes to these two names and decides where the data goes.
/// </summary>
public static class AgentTelemetry
{
    /// <summary>Name of the <see cref="ActivitySource"/> that carries the agent's spans.</summary>
    public const string SourceName = "SupplyChainAgent";

    /// <summary>Name of the <see cref="Meter"/> that carries the agent's metrics.</summary>
    public const string MeterName = "SupplyChainAgent";

    /// <summary>Span name of one chat turn: one question and the answer to it.</summary>
    public const string ChatSpanName = "chat";

    /// <summary>Prefix of the span name of one tool call; the tool's name is appended to it.</summary>
    public const string ToolSpanName = "tool";

    /// <summary>The agent's spans. A host subscribes with <c>AddSource(SourceName)</c>.</summary>
    public static ActivitySource Source { get; } = new(SourceName);

    /// <summary>The agent's metrics. A host subscribes with <c>AddMeter(MeterName)</c>.</summary>
    public static Meter Meter { get; } = new(MeterName);

    /// <summary>How long one chat turn took, from question to answer and tool calls included.</summary>
    public static Histogram<double> ChatDuration { get; } = Meter.CreateHistogram<double>(
        "agent.chat.duration",
        unit: "ms",
        description: "Duration of one chat turn.");

    /// <summary>Input tokens the model billed for chat turns.</summary>
    public static Counter<long> InputTokens { get; } = Meter.CreateCounter<long>(
        "agent.tokens.input",
        unit: "{token}",
        description: "Input tokens billed for chat turns.");

    /// <summary>Output tokens the model billed for chat turns.</summary>
    public static Counter<long> OutputTokens { get; } = Meter.CreateCounter<long>(
        "agent.tokens.output",
        unit: "{token}",
        description: "Output tokens billed for chat turns.");

    /// <summary>Starts the span for one chat turn.</summary>
    /// <param name="conversationId">Conversation the turn belongs to.</param>
    /// <returns>
    /// The started span, which becomes the parent of every tool call the turn makes; or <c>null</c> when
    /// nothing is listening, in which case the caller can keep using <c>?.</c> and pay nothing.
    /// </returns>
    public static Activity? StartChatSpan(string conversationId)
    {
        var activity = Source.StartActivity(ChatSpanName, ActivityKind.Server);
        activity?.SetTag("chat.conversation_id", conversationId);
        return activity;
    }

    /// <summary>Records one finished chat turn: how long it took and the tokens it used.</summary>
    /// <param name="latency">Wall-clock time of the turn, tool calls included.</param>
    /// <param name="usage">Token counts the provider reported, or <c>null</c> when it reported none.</param>
    public static void RecordChat(TimeSpan latency, UsageDetails? usage)
    {
        ChatDuration.Record(latency.TotalMilliseconds);

        if (usage?.InputTokenCount is { } input)
        {
            InputTokens.Add(input);
        }

        if (usage?.OutputTokenCount is { } output)
        {
            OutputTokens.Add(output);
        }
    }
}
