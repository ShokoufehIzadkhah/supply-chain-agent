using Agent.Core;
using Agent.Tools;
using Agent.Tools.Rag;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

// Standard input and output carry the JSON-RPC protocol, so the working directory a client picks is
// not necessarily the application directory: anchor configuration and the policy documents to the
// executable, exactly as the console host does.
var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true)
    .AddUserSecrets<Program>(optional: true)
    .AddEnvironmentVariables()
    .Build();

// The MCP server only exposes tools, so it needs no chat model, but it does need the embedding
// generator and the ERP client that sit behind those tools. They come from a container of their own
// because the tools have to exist before the MCP host can be configured with them.
using var provider = new ServiceCollection()
    .AddOrderAssistant(configuration)
    .BuildServiceProvider();

// Index the policy documents before the first call, so SearchPolicies always has data. Only
// documents whose content hash changed are chunked and embedded again.
var policyOptions = provider.GetRequiredService<PolicyRagOptions>();
await provider.GetRequiredService<PolicyIndexer>().RebuildAsync(policyOptions.PolicyDirectory);

// The very same Agent.Tools methods the console assistant uses. Agent.Core owns the list, so this
// host adds no second definition of a tool name, description or parameter schema, and Agent.Tools
// itself stays free of any MCP dependency.
var tools = AgentToolset
    .ToFunctions(provider.GetRequiredService<OrderTools>(), provider.GetRequiredService<PolicyTools>())
    .Select(function => McpServerTool.Create(function, AnnotationsFor(function.Name)))
    .ToList();

var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory,
});

// stdout is reserved for the protocol. Anything logged there would corrupt the JSON-RPC stream, so
// every log line goes to stderr instead; the client shows it in its own log.
builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools(tools);

await builder.Build().RunAsync();

// The delivery date proposal is the only tool that writes anything, and even that only files a
// request a human supervisor still has to approve, so nothing here can change the ERP by itself.
static McpServerToolCreateOptions AnnotationsFor(string toolName) => new()
{
    ReadOnly = toolName != nameof(OrderTools.ProposeDeliveryDateChange),
    Destructive = false,
};
