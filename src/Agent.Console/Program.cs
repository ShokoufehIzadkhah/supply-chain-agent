using Agent.Console;
using Agent.Tools;
using Agent.Tools.Rag;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
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
    .AddSingleton(ChatClientFactory.CreateEmbeddingGenerator(configuration))
    .AddAgentTools(configuration)
    .AddPolicyRag(configuration)
    .BuildServiceProvider();

// Build the policy index before the first question. Only documents whose content hash changed are
// chunked and embedded again, so a restart with unchanged policies costs nothing.
var policyOptions = provider.GetRequiredService<PolicyRagOptions>();
await provider.GetRequiredService<PolicyIndexer>().RebuildAsync(policyOptions.PolicyDirectory);

var orderTools = provider.GetRequiredService<OrderTools>();
var policyTools = provider.GetRequiredService<PolicyTools>();

// Expose the OrderTools methods as function tools the model can call. Each one is wrapped so the
// console can report the tool name, its arguments and the duration in grey as the call happens.
var tools = new List<AITool>
{
    new ToolCallWriter(AIFunctionFactory.Create((Func<int, Task<string>>)orderTools.GetOrderStatus)),
    new ToolCallWriter(AIFunctionFactory.Create((Func<string, string?, Task<string>>)orderTools.FindOrdersByCustomer)),
    new ToolCallWriter(AIFunctionFactory.Create((Func<string, Task<string>>)orderTools.CheckInventory)),
    new ToolCallWriter(AIFunctionFactory.Create((Func<int, Task<string>>)orderTools.GetShipmentInfo)),
    new ToolCallWriter(AIFunctionFactory.Create(
        (Func<int, DateOnly, string, Task<string>>)orderTools.ProposeDeliveryDateChange)),
    new ToolCallWriter(AIFunctionFactory.Create((Func<string, Task<string>>)policyTools.SearchPolicies)),
};

var systemPrompt = await File.ReadAllTextAsync(
    Path.Combine(AppContext.BaseDirectory, "Prompts", "system.md"));

AIAgent agent = ChatClientFactory.Create(configuration).AsAIAgent(
    instructions: systemPrompt,
    name: "OrderAssistant",
    description: "Answers customer service questions about orders, inventory and shipments.",
    tools: tools,
    loggerFactory: provider.GetService<ILoggerFactory>());

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
