using Agent.Tools;
using Agent.Tools.Rag;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Agent.Core;

/// <summary>Registers the parts of the order assistant that every host needs.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the embedding generator for the configured provider, the ERP tools and the policy
    /// index and search services. A host that talks to a model adds the chat client itself, so a host
    /// that only exposes tools — the MCP server — needs no chat model in its configuration.
    /// </summary>
    /// <param name="services">Service collection to register into.</param>
    /// <param name="configuration">
    /// Configuration holding the <c>AI</c>, <c>ErpApi</c>, <c>ApiKeys</c> and optional <c>Rag</c> keys.
    /// </param>
    /// <returns>The same service collection, so calls can be chained.</returns>
    public static IServiceCollection AddOrderAssistant(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton(ChatClientFactory.CreateEmbeddingGenerator(configuration));
        services.AddAgentTools(configuration);
        services.AddPolicyRag(configuration);

        return services;
    }
}
