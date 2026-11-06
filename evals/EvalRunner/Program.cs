using System.Diagnostics;
using Agent.Core;
using Agent.Tools;
using Agent.Tools.Rag;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// The eval set and report types live in this project's EvalRunner namespace; top-level statements sit
// in the global namespace, so the import is explicit.
using EvalRunner;

// A run is a batch experiment, not an interactive host: it never prompts, it loops over the eval set
// once and writes the report. The host wiring still matches Agent.Console, so the cases exercise the
// same agent, tools and policy index a user would talk to.
// Configuration order: appsettings.json (safe defaults), user-secrets (local secrets such as
// AI:ApiKey), then environment variables (CI). Secrets are never written to appsettings.json.
var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true)
    .AddUserSecrets<Program>(optional: true)
    .AddEnvironmentVariables()
    .Build();

using var provider = new ServiceCollection()
    .AddLogging(logging => logging
        .AddConfiguration(configuration.GetSection("Logging"))
        .AddSimpleConsole(options => options.SingleLine = true))
    .AddOrderAssistant(configuration)
    .BuildServiceProvider();

// Build the policy index before the first question. Policy cases call SearchPolicies, so an empty
// index would answer them from the model's memory instead of from the documents.
var policyOptions = provider.GetRequiredService<PolicyRagOptions>();
await provider.GetRequiredService<PolicyIndexer>().RebuildAsync(policyOptions.PolicyDirectory);

// The eval set is repository data. Prefer the checked-in copy, and fall back to the one copied next to
// the executable when the runner is started outside the repository.
var repositoryRoot = EvalSet.FindRepositoryRoot();
var evalSetPath = Path.Combine(repositoryRoot, "evals", "eval-set.json");

if (!File.Exists(evalSetPath))
{
    evalSetPath = Path.Combine(AppContext.BaseDirectory, "eval-set.json");
}

var cases = EvalSet.Load(evalSetPath);

// The tools are wrapped once. Every call appends its name to this list, which each case clears before
// it runs, so the report can say which tools an answer actually used.
var toolCalls = new List<string>();

var tools = AgentToolset
    .ToFunctions(provider.GetRequiredService<OrderTools>(), provider.GetRequiredService<PolicyTools>())
    .Select(function => new ToolCallRecorder(function, call =>
    {
        toolCalls.Add(call.Name);
        return ValueTask.CompletedTask;
    }));

AIAgent agent = OrderAssistant.Create(configuration, tools, provider.GetService<ILoggerFactory>());

Console.WriteLine($"Running {cases.Count} eval cases against {agent.Name}.");
Console.WriteLine();

var results = new List<CaseResult>(cases.Count);

foreach (var testCase in cases)
{
    Console.WriteLine($"[{testCase.Id}] {testCase.Question}");

    toolCalls.Clear();

    // A fresh session per case is what makes the run a test rather than a chat: the agent must answer
    // from the question alone, with no history from the case before it.
    AgentSession session = await agent.CreateSessionAsync();
    var stopwatch = Stopwatch.StartNew();

    string answer;
    long? tokens;
    IReadOnlyList<string> failures;

    try
    {
        var response = await agent.RunAsync(testCase.Question, session);
        stopwatch.Stop();

        answer = response.Text ?? string.Empty;
        tokens = response.Usage?.TotalTokenCount;
        failures = testCase.Check(answer, toolCalls);
    }
    catch (Exception ex)
    {
        // A provider or tool failure is a failed case, not a crash: it must still appear in the report.
        stopwatch.Stop();
        answer = string.Empty;
        tokens = null;
        failures = [$"run failed: {ex.Message}"];
    }

    var passed = failures.Count == 0;
    results.Add(new CaseResult(testCase, passed, [.. toolCalls], stopwatch.Elapsed, tokens, failures));

    Console.WriteLine(passed
        ? "  -> pass"
        : $"  -> FAIL: {string.Join("; ", failures)}");
    Console.WriteLine();
}

var ranAt = DateTimeOffset.Now;
var markdown = EvalReport.Render(results, ranAt);

// One report per day: a rerun on the same day replaces the earlier file so the directory stays small.
var resultsDirectory = Path.Combine(repositoryRoot, "evals", "results");
Directory.CreateDirectory(resultsDirectory);

var reportPath = Path.Combine(resultsDirectory, $"{ranAt:yyyy-MM-dd}.md");
await File.WriteAllTextAsync(reportPath, markdown);

Console.WriteLine(markdown);
Console.WriteLine($"Report written to {reportPath}");
