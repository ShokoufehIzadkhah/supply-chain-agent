using System.Text.Json;

namespace EvalRunner;

/// <summary>
/// One eval case: a question, the tools that must be called to answer it, and the text the answer must
/// or must not contain. <c>mustContain</c> holds what a correct answer adds from the tool results;
/// <c>mustNotContain</c> holds what a wrong answer would say — an invented status, a claimed approval.
/// </summary>
/// <param name="Id">Stable name of the case, used in the report.</param>
/// <param name="Question">The user turn sent to the agent in a fresh conversation.</param>
/// <param name="ExpectedTools">
/// Tool names that must all have been called. An empty list places no constraint on the tools.
/// </param>
/// <param name="MustContain">Text the answer must contain, matched case-insensitively.</param>
/// <param name="MustNotContain">Text the answer must not contain, matched case-insensitively.</param>
/// <param name="Category">Group the case belongs to, for example <c>policy-violation</c>.</param>
public sealed record EvalCase(
    string Id,
    string Question,
    IReadOnlyList<string> ExpectedTools,
    IReadOnlyList<string> MustContain,
    IReadOnlyList<string> MustNotContain,
    string Category)
{
    /// <summary>Compares an answer and the tools it called against the rules of this case.</summary>
    /// <param name="answer">Text the agent answered with.</param>
    /// <param name="toolNames">Names of the tools the answer called.</param>
    /// <returns>One message per rule that was broken; an empty list when the case passed.</returns>
    public IReadOnlyList<string> Check(string answer, IReadOnlyList<string> toolNames)
    {
        var failures = new List<string>();

        var missingTools = ExpectedTools
            .Where(expected => !toolNames.Contains(expected, StringComparer.OrdinalIgnoreCase))
            .ToList();

        if (missingTools.Count > 0)
        {
            failures.Add($"did not call {string.Join(", ", missingTools)}");
        }

        foreach (var text in MustContain.Where(text => !Contains(answer, text)))
        {
            failures.Add($"answer does not contain \"{text}\"");
        }

        foreach (var text in MustNotContain.Where(text => Contains(answer, text)))
        {
            failures.Add($"answer contains \"{text}\"");
        }

        return failures;
    }

    private static bool Contains(string answer, string text)
        => answer.Contains(text, StringComparison.OrdinalIgnoreCase);
}

/// <summary>Reads the eval set and locates the repository, so a run from any directory finds both.</summary>
public static class EvalSet
{
    /// <summary>Marker file that identifies the repository root from a directory and its parents.</summary>
    private const string SolutionFile = "SupplyChainAgent.sln";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Reads the cases from a JSON array file.</summary>
    /// <param name="path">Path to the eval set.</param>
    /// <returns>The cases, in file order.</returns>
    /// <exception cref="InvalidOperationException">The file does not hold a non-empty array of cases.</exception>
    public static IReadOnlyList<EvalCase> Load(string path)
    {
        using var stream = File.OpenRead(path);
        var cases = JsonSerializer.Deserialize<List<EvalCase>>(stream, JsonOptions);

        return cases is { Count: > 0 }
            ? cases
            : throw new InvalidOperationException($"'{path}' does not contain any eval cases.");
    }

    /// <summary>
    /// Finds the repository root by walking up from the working directory and then from the application
    /// directory until the solution file is found. The report and the eval set live in the repository,
    /// not in <c>bin/</c>, so the runner needs the root rather than its own output directory.
    /// </summary>
    /// <returns>The repository root, or the working directory when no marker file was found.</returns>
    public static string FindRepositoryRoot()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            for (var directory = new DirectoryInfo(start);
                 directory is not null;
                 directory = directory.Parent)
            {
                if (File.Exists(Path.Combine(directory.FullName, SolutionFile)))
                {
                    return directory.FullName;
                }
            }
        }

        return Directory.GetCurrentDirectory();
    }
}
