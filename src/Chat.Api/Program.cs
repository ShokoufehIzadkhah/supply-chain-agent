using Agent.Core;
using Agent.Tools;
using Agent.Tools.Rag;
using Chat.Api.ChangeRequests;
using Chat.Api.Chat;
using Chat.Api.Telemetry;

var builder = WebApplication.CreateBuilder(args);

// Traces and metrics for the agent: a span per chat request with a child span per tool call, and
// latency and token metrics. Console by default, Application Insights when the connection string is set.
builder.AddChatTelemetry();

// How many question and answer pairs one conversation keeps in memory.
const int maxHistoryTurns = 20;

// Which browser origin may call this API: the chat UI. CORS keeps a browser from calling the API from
// another page; it is not authentication and says nothing about who may call the API outside a browser.
const string webCorsPolicy = "web";
var webOrigin = builder.Configuration["Web:Origin"] ?? "http://localhost:3000";

builder.Services.AddCors(options => options.AddPolicy(webCorsPolicy, policy => policy
    .WithOrigins(webOrigin)
    .AllowAnyHeader()
    .AllowAnyMethod()));

// Everything the console host has: the embedding generator, the ERP tools and the policy index.
// Chat.Api adds the chat client itself, because only a host that talks to a model needs AI:ChatModel.
builder.Services.AddOrderAssistant(builder.Configuration);

// Also a factory: the client is created on first use, after the host has merged every configuration
// source, so settings added later (deployment environment variables, test overrides) are picked up.
builder.Services.AddSingleton(_ => ChatClientFactory.Create(builder.Configuration));
builder.Services.AddSingleton(new ConversationStore(maxHistoryTurns));
builder.Services.AddSupervisorErpClient(builder.Configuration);

var app = builder.Build();

// The ERP clients read their base URL and API key when they are built, so build them once here: a
// missing key or a bad base URL stops the host at startup instead of on the first question or approval.
_ = app.Services.GetRequiredService<OrderTools>();
_ = app.Services.GetRequiredService<SupervisorErpClient>();

// Index the policy documents before the first question, so SearchPolicies always answers from the
// documents currently on disk.
await app.Services
    .GetRequiredService<PolicyIndexer>()
    .RebuildAsync(app.Services.GetRequiredService<PolicyRagOptions>().PolicyDirectory);

app.UseCors(webCorsPolicy);
app.MapChatEndpoints();
app.MapChangeRequestEndpoints();

app.Run();

// Named so the integration tests can host the app with WebApplicationFactory<Program>.
public partial class Program;
