using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.AI;

namespace Agent.Console;

/// <summary>
/// Wraps an <see cref="AIFunction"/> so that every invocation is reported on the console in grey with
/// the tool name, the arguments it received and how long it took. The report is written while the
/// agent is still working, so it always appears before the answer text.
/// </summary>
/// <param name="innerFunction">The tool to wrap and report on.</param>
internal sealed class ToolCallWriter(AIFunction innerFunction) : DelegatingAIFunction(innerFunction)
{
    private static readonly Lock ConsoleGate = new();

    /// <inheritdoc />
    protected override async ValueTask<object?> InvokeCoreAsync(
        AIFunctionArguments arguments,
        CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();

        try
        {
            return await base.InvokeCoreAsync(arguments, cancellationToken);
        }
        finally
        {
            WriteReport(Name, arguments, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }
    }

    private static void WriteReport(string name, AIFunctionArguments arguments, double milliseconds)
    {
        var args = arguments.Count == 0
            ? "{}"
            : JsonSerializer.Serialize(arguments.ToDictionary(pair => pair.Key, pair => pair.Value));

        lock (ConsoleGate)
        {
            var previous = System.Console.ForegroundColor;
            System.Console.ForegroundColor = ConsoleColor.DarkGray;
            System.Console.WriteLine($"  [tool] {name}({args}) in {milliseconds:F0} ms");
            System.Console.ForegroundColor = previous;
        }
    }
}
