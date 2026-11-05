using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Agent.Tools;

/// <summary>Registration helpers for the agent tools.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>Configuration key holding the agent's API key.</summary>
    private const string AgentKeyConfigKey = "ApiKeys:Agent";

    /// <summary>Header the ERP API reads the API key from.</summary>
    private const string ApiKeyHeaderName = "X-Api-Key";

    /// <summary>
    /// Registers the typed <see cref="ErpApiClient"/> (base address from the configuration key
    /// <c>ErpApi:BaseUrl</c>, agent key from <c>ApiKeys:Agent</c>) together with the
    /// <see cref="OrderTools"/> agent tools.
    /// </summary>
    /// <param name="services">Service collection to register into.</param>
    /// <param name="configuration">Configuration that provides <c>ErpApi:BaseUrl</c> and <c>ApiKeys:Agent</c>.</param>
    /// <returns>The same service collection, so calls can be chained.</returns>
    /// <exception cref="InvalidOperationException">
    /// <c>ErpApi:BaseUrl</c> is missing or not an absolute URI, or <c>ApiKeys:Agent</c> is missing.
    /// </exception>
    public static IServiceCollection AddAgentTools(this IServiceCollection services, IConfiguration configuration)
    {
        var baseUrl = configuration["ErpApi:BaseUrl"];

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new InvalidOperationException(
                "Configuration value 'ErpApi:BaseUrl' is required but was not set.");
        }

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException(
                $"Configuration value 'ErpApi:BaseUrl' ('{baseUrl}') is not a valid absolute URI.");
        }

        // Fail fast: every ERP call carries the agent key, so a missing key is a configuration
        // mistake that must surface at startup rather than as a 401 on the first question.
        var agentKey = configuration[AgentKeyConfigKey];

        if (string.IsNullOrWhiteSpace(agentKey))
        {
            throw new InvalidOperationException(
                $"Configuration value '{AgentKeyConfigKey}' is required but was not set. " +
                "Set it with 'dotnet user-secrets set \"ApiKeys:Agent\" \"<key>\"' or an environment variable.");
        }

        // Guarantee a trailing slash so relative request paths are appended to, not replaced.
        var normalized = uri.AbsoluteUri.EndsWith('/') ? uri.AbsoluteUri : $"{uri.AbsoluteUri}/";

        services.AddHttpClient<ErpApiClient>(client =>
        {
            client.BaseAddress = new Uri(normalized);
            client.DefaultRequestHeaders.Add(ApiKeyHeaderName, agentKey);
        });

        services.AddTransient<OrderTools>();

        return services;
    }
}
