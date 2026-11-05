using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Agent.Tools;

/// <summary>Registration helpers for the agent tools.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the typed <see cref="ErpApiClient"/> (base address from the configuration key
    /// <c>ErpApi:BaseUrl</c>) together with the <see cref="OrderTools"/> agent tools.
    /// </summary>
    /// <param name="services">Service collection to register into.</param>
    /// <param name="configuration">Configuration that provides <c>ErpApi:BaseUrl</c>.</param>
    /// <returns>The same service collection, so calls can be chained.</returns>
    /// <exception cref="InvalidOperationException"><c>ErpApi:BaseUrl</c> is missing or not an absolute URI.</exception>
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

        // Guarantee a trailing slash so relative request paths are appended to, not replaced.
        var normalized = uri.AbsoluteUri.EndsWith('/') ? uri.AbsoluteUri : $"{uri.AbsoluteUri}/";

        services.AddHttpClient<ErpApiClient>(client => client.BaseAddress = new Uri(normalized));
        services.AddTransient<OrderTools>();

        return services;
    }
}
