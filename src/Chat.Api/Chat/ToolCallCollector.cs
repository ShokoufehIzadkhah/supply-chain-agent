using System.Text.Json;
using Agent.Core;
using Agent.Tools.Rag;

namespace Chat.Api.Chat;

/// <summary>
/// Collects the tool calls one answer made. Every request builds its own collector, so two conversations
/// answering at the same time cannot attribute each other's calls.
/// </summary>
/// <param name="onCallCompleted">
/// Optional callback invoked as each call finishes. The streaming endpoint uses it to report progress
/// while the answer is still being written.
/// </param>
public sealed class ToolCallCollector(Func<ToolCallDto, CancellationToken, ValueTask>? onCallCompleted = null)
{
    private readonly Lock _gate = new();
    private readonly List<ToolCallRecord> _calls = [];

    /// <summary>Records a finished tool call. Handed to <see cref="ToolCallRecorder"/> as its callback.</summary>
    /// <param name="call">The finished call.</param>
    public async ValueTask AddAsync(ToolCallRecord call)
    {
        lock (_gate)
        {
            _calls.Add(call);
        }

        if (onCallCompleted is not null)
        {
            await onCallCompleted(ToDto(call), CancellationToken.None);
        }
    }

    /// <summary>The calls the answer made, in the order they finished.</summary>
    /// <returns>The <c>toolCalls</c> array of the response.</returns>
    public IReadOnlyList<ToolCallDto> ToToolCalls()
    {
        lock (_gate)
        {
            return [.. _calls.Select(ToDto)];
        }
    }

    /// <summary>
    /// The policy documents the answer is grounded in, as <c>file §section</c>. They come from the policy
    /// searches that actually ran rather than from the text the model wrote, and repeats are dropped.
    /// </summary>
    /// <returns>The <c>sources</c> array of the response.</returns>
    public IReadOnlyList<string> ToSources()
    {
        List<ToolCallRecord> calls;

        lock (_gate)
        {
            calls = [.. _calls];
        }

        return
        [
            .. calls
                .Where(call => call.Name == nameof(PolicyTools.SearchPolicies))
                .Select(call => ReadSearchResult(call.Result))
                .Where(result => result is not null)
                .SelectMany(result => result!.Matches)
                .Select(match => string.IsNullOrWhiteSpace(match.Section)
                    ? match.Source
                    : $"{match.Source} §{match.Section}")
                .Distinct(StringComparer.OrdinalIgnoreCase),
        ];
    }

    private static ToolCallDto ToDto(ToolCallRecord call) => new(call.Name, call.Arguments, call.DurationMs);

    private static PolicySearchResult? ReadSearchResult(object? result) => result switch
    {
        // A tool that returns a string arrives here as a JSON string value: AIFunctionFactory marshals
        // every result that is not AIContent to a JsonElement before a wrapper sees it.
        JsonElement { ValueKind: JsonValueKind.String } element => PolicyTools.ParseResult(element.GetString()!),
        string text => PolicyTools.ParseResult(text),
        _ => null,
    };
}
