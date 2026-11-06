using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.AI;

namespace Agent.Core;

/// <summary>What one tool call did, so a host can report it to the user.</summary>
/// <param name="Name">Name of the tool that ran.</param>
/// <param name="Arguments">Arguments the call received, as JSON.</param>
/// <param name="DurationMs">Wall-clock duration of the call, in milliseconds.</param>
/// <param name="Result">
/// Value the tool returned. Functions built by <see cref="AIFunctionFactory"/> marshal results that are
/// not <see cref="AIContent"/> to <see cref="JsonElement"/>, so this is usually a <see cref="JsonElement"/>
/// rather than the original object.
/// </param>
public sealed record ToolCallRecord(string Name, JsonElement Arguments, double DurationMs, object? Result);

/// <summary>
/// Wraps an <see cref="AIFunction"/> so that every invocation is reported with its name, arguments and
/// duration. Both hosts wrap the shared tool surface with it: the console prints the report as each call
/// happens, and Chat.Api returns it as the <c>toolCalls</c> array of an answer.
/// </summary>
/// <param name="innerFunction">The tool to wrap and report on.</param>
/// <param name="onCompleted">
/// Called after each invocation, whether it succeeded or threw. Awaited before the tool's result is
/// returned, so a host that streams the report to a client cannot write after the answer.
/// </param>
public sealed class ToolCallRecorder(
    AIFunction innerFunction,
    Func<ToolCallRecord, ValueTask>? onCompleted = null) : DelegatingAIFunction(innerFunction)
{
    /// <inheritdoc />
    protected override async ValueTask<object?> InvokeCoreAsync(
        AIFunctionArguments arguments,
        CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        object? result = null;

        try
        {
            result = await base.InvokeCoreAsync(arguments, cancellationToken);
            return result;
        }
        finally
        {
            if (onCompleted is not null)
            {
                var record = new ToolCallRecord(
                    Name,
                    SerializeArguments(arguments),
                    Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                    result);

                await onCompleted(record);
            }
        }
    }

    private static JsonElement SerializeArguments(AIFunctionArguments arguments)
        => arguments.Count == 0
            ? JsonSerializer.SerializeToElement(new Dictionary<string, object?>())
            : JsonSerializer.SerializeToElement(arguments.ToDictionary(pair => pair.Key, pair => pair.Value));
}
