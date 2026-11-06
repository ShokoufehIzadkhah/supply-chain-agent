using Agent.Tools.Rag;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Chat.Api.Tests;

/// <summary>
/// Boots Chat.Api in-process with the three services that would otherwise leave the process — the chat
/// client, the embedding generator and the vector store — replaced by fakes. Everything else is what
/// the real host registers, so the endpoints under test run against the real agent, prompt and tools.
/// </summary>
public sealed class ChatApiFactory : WebApplicationFactory<Program>
{
    /// <summary>The only answer the stub model gives.</summary>
    public const string AnswerText = "Order 4711 leaves the plant on 12 November.";

    private readonly StubChatClient _chatClient = new(AnswerText);

    /// <summary>The stub model, so a test can see what the agent asked it and with which history.</summary>
    public StubChatClient ChatClient => _chatClient;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        // Startup fails without these keys, and none of them is a real secret: the fakes below never
        // use the provider, the model names or the API keys.
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AI:Provider"] = "OpenAI",
                ["AI:ApiKey"] = "test-key",
                ["AI:ChatModel"] = "test-chat-model",
                ["AI:EmbeddingModel"] = "test-embedding-model",
                ["ErpApi:BaseUrl"] = "http://localhost:5187/",
                ["ApiKeys:Agent"] = "test-agent-key",
                ["ApiKeys:Supervisor"] = "test-supervisor-key",
            }));

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IChatClient>();
            services.RemoveAll<IEmbeddingGenerator<string, Embedding<float>>>();
            services.RemoveAll<IVectorStore>();

            services.AddSingleton<IChatClient>(_chatClient);
            services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(new FakeEmbeddingGenerator());

            // Also keeps the index in memory, so a test run leaves no policy-index.db behind.
            services.AddSingleton<IVectorStore>(new InMemoryVectorStore());
        });
    }
}
