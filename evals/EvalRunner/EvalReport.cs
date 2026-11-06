using System.Globalization;
using System.Text;

namespace EvalRunner;

/// <summary>What one eval case did when it ran.</summary>
/// <param name="Case">The case that ran.</param>
/// <param name="Passed">Whether every tool and text rule of the case held.</param>
/// <param name="ToolNames">Tools the answer called, in the order they finished.</param>
/// <param name="Latency">Wall-clock time of the whole turn, tool calls included.</param>
/// <param name="Tokens">Total tokens the model used, or <c>null</c> when the provider reported none.</param>
/// <param name="Failures">Rules the answer broke; empty when the case passed.</param>
public sealed record CaseResult(
    EvalCase Case,
    bool Passed,
    IReadOnlyList<string> ToolNames,
    TimeSpan Latency,
    long? Tokens,
    IReadOnlyList<string> Failures);

/// <summary>Renders a run as a Markdown report: one row per case, then the totals.</summary>
public static class EvalReport
{
    /// <summary>Renders the cases that ran, in order, with the totals underneath.</summary>
    /// <param name="results">One result per case, in the order the cases ran.</param>
    /// <param name="ranAt">When the run started, used for the title and the heading.</param>
    /// <returns>The report as Markdown.</returns>
    public static string Render(IReadOnlyList<CaseResult> results, DateTimeOffset ranAt)
    {
        var passed = results.Count(result => result.Passed);
        var report = new StringBuilder();

        report.AppendLine($"# Order Assistant eval — {ranAt:yyyy-MM-dd}");
        report.AppendLine();
        report.AppendLine(
            $"Ran {results.Count} cases at {ranAt:yyyy-MM-dd HH:mm} ({passed} passed).");
        report.AppendLine();
        report.AppendLine("| # | Case | Category | Result | Tools called | Latency | Tokens | Notes |");
        report.AppendLine("| --- | --- | --- | --- | --- | --- | --- | --- |");

        for (var index = 0; index < results.Count; index++)
        {
            var result = results[index];

            report.AppendLine(Row(
                (index + 1).ToString(CultureInfo.InvariantCulture),
                result.Case.Id,
                result.Case.Category,
                result.Passed ? "pass" : "fail",
                result.ToolNames.Count == 0 ? "—" : string.Join(", ", result.ToolNames),
                Latency(result.Latency),
                Tokens(result.Tokens),
                string.Join("; ", result.Failures)));
        }

        var tokenResults = results.Where(result => result.Tokens.HasValue).ToList();
        var averageLatency = results.Count == 0
            ? 0
            : results.Average(result => result.Latency.TotalMilliseconds);
        var averageTokens = tokenResults.Count == 0
            ? (double?)null
            : tokenResults.Average(result => (double)result.Tokens!.Value);

        report.AppendLine();
        report.AppendLine("## Totals");
        report.AppendLine();
        report.AppendLine("| Metric | Value |");
        report.AppendLine("| --- | --- |");
        report.AppendLine(Row("Cases", results.Count.ToString(CultureInfo.InvariantCulture)));
        report.AppendLine(Row("Passed", passed.ToString(CultureInfo.InvariantCulture)));
        report.AppendLine(Row("Pass rate", Percentage(results.Count == 0 ? 0 : (double)passed / results.Count)));
        report.AppendLine(Row("Avg latency", TimeSpan.FromMilliseconds(averageLatency).TotalMilliseconds
            .ToString("N0", CultureInfo.InvariantCulture) + " ms"));
        report.AppendLine(Row("Avg tokens", averageTokens is { } tokens
            ? tokens.ToString("N0", CultureInfo.InvariantCulture)
            : "n/a"));

        return report.ToString();
    }

    private static string Row(params string[] cells)
        => $"| {string.Join(" | ", cells.Select(Escape))} |";

    private static string Latency(TimeSpan latency)
        => latency.TotalMilliseconds.ToString("N0", CultureInfo.InvariantCulture) + " ms";

    private static string Tokens(long? tokens)
        => tokens?.ToString("N0", CultureInfo.InvariantCulture) ?? "n/a";

    private static string Percentage(double fraction)
        => (fraction * 100).ToString("0.0", CultureInfo.InvariantCulture) + "%";

    /// <summary>Keeps a cell on one row: a pipe would open a new column, a newline would end the row.</summary>
    private static string Escape(string cell)
        => cell.Replace("|", "\\|", StringComparison.Ordinal)
            .ReplaceLineEndings(" ");
}
