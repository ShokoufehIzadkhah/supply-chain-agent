using System.ClientModel;
using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI;

namespace Agent.Console;

/// <summary>
/// Builds the <see cref="IChatClient"/> the agent talks to, from the <c>AI</c> configuration section.
/// Switching provider is a configuration change only:
/// <list type="bullet">
///   <item><description><c>AzureOpenAI</c> — Azure OpenAI (endpoint, key and deployment name).</description></item>
///   <item><description><c>GitHubModels</c> — GitHub Models, an OpenAI-compatible endpoint.</description></item>
///   <item><description><c>OpenAI</c> — the public OpenAI API.</description></item>
/// </list>
/// </summary>
internal static class ChatClientFactory
{
    private const string DefaultOpenAiEndpoint = "https://api.openai.com/v1";
    private const string DefaultGitHubModelsEndpoint = "https://models.github.ai/inference";

    /// <summary>Creates the chat client described by <paramref name="configuration"/>.</summary>
    /// <param name="configuration">Configuration containing the <c>AI</c> section.</param>
    /// <returns>A chat client for the configured provider.</returns>
    /// <exception cref="InvalidOperationException">A required setting is missing, or the provider is unknown.</exception>
    public static IChatClient Create(IConfiguration configuration)
    {
        var provider = Required(configuration, "AI:Provider");
        var chatModel = Required(configuration, "AI:ChatModel");
        var apiKey = Required(configuration, "AI:ApiKey");

        return provider.ToLowerInvariant() switch
        {
            // Azure needs the Azure SDK client: it authenticates with the "api-key" header and takes
            // the deployment name rather than a model name.
            "azureopenai" => new AzureOpenAIClient(
                    new Uri(Required(configuration, "AI:Endpoint")),
                    new ApiKeyCredential(apiKey))
                .GetChatClient(chatModel)
                .AsIChatClient(),

            // GitHub Models speaks the OpenAI protocol, so the plain OpenAI client is enough.
            "githubmodels" => new OpenAIClient(
                    new ApiKeyCredential(apiKey),
                    new OpenAIClientOptions
                    {
                        Endpoint = new Uri(configuration["AI:Endpoint"] ?? DefaultGitHubModelsEndpoint),
                    })
                .GetChatClient(chatModel)
                .AsIChatClient(),

            "openai" => new OpenAIClient(
                    new ApiKeyCredential(apiKey),
                    new OpenAIClientOptions
                    {
                        Endpoint = new Uri(configuration["AI:Endpoint"] ?? DefaultOpenAiEndpoint),
                    })
                .GetChatClient(chatModel)
                .AsIChatClient(),

            _ => throw new InvalidOperationException(
                $"Configuration value 'AI:Provider' ('{provider}') is not supported. " +
                "Use 'AzureOpenAI', 'GitHubModels' or 'OpenAI'."),
        };
    }

    private static string Required(IConfiguration configuration, string key)
        => configuration[key] is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException(
                $"Configuration value '{key}' is required but was not set. "
                + "Set it with 'dotnet user-secrets' or an environment variable.");
}
