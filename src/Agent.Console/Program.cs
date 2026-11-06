using Agent.Core;
using Agent.Tools;
using Agent.Tools.Rag;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// Configuration order: appsettings.json (safe defaults), user-secrets (local secrets such as
// AI:ApiKey), then environment variables (deployments). Secrets are never written to appsettings.json.
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

// Build the policy index before the first question. Only documents whose content hash changed are
// chunked and embedded again, so a restart with unchanged policies costs nothing.
var policyOptions = provider.GetRequiredService<PolicyRagOptions>();
await provider.GetRequiredService<PolicyIndexer>().RebuildAsync(policyOptions.PolicyDirectory);

// The tool surface comes from Agent.Core, which is also what the MCP server exposes: one
// definition of every name, description and parameter schema. The console adds only the reporting
// wrapper that prints the name, the arguments and the duration in grey as the call happens.
var consoleGate = new Lock();

void WriteToolCall(ToolCallRecord call)
{
    lock (consoleGate)
    {
        var previous = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"  [tool] {call.Name}({call.Arguments.GetRawText()}) in {call.DurationMs:F0} ms");
        Console.ForegroundColor = previous;
    }
}

var tools = AgentToolset
    .ToFunctions(provider.GetRequiredService<OrderTools>(), provider.GetRequiredService<PolicyTools>())
    .Select(function => new ToolCallRecorder(function, call =>
    {
        WriteToolCall(call);
        return ValueTask.CompletedTask;
    }));

AIAgent agent = OrderAssistant.Create(configuration, tools, provider.GetService<ILoggerFactory>());

// One session carries the conversation history, so the agent remembers earlier turns.
AgentSession session = await agent.CreateSessionAsync();

Console.WriteLine("Order Assistant - ask about orders, inventory or shipments. Type 'exit' to quit.");
Console.WriteLine();

while (true)
{
    Console.Write("you> ");
    var question = Console.ReadLine();

    if (question is null || question.Trim().Equals("exit", StringComparison.OrdinalIgnoreCase))
    {
        break;
    }

    if (string.IsNullOrWhiteSpace(question))
    {
        continue;
    }

    try
    {
        var response = await agent.RunAsync(question, session);
        Console.WriteLine();
        Console.WriteLine(response.Text);
        Console.WriteLine();
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"Something went wrong: {ex.Message}");
        Console.ResetColor();
        Console.WriteLine();
    }
}

Console.WriteLine("Bye.");
