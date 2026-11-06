using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Chat.Api.ChangeRequests;

/// <summary>Registration helper for the supervisor's view of the ERP API.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>Configuration key holding the supervisor's API key.</summary>
    private const string SupervisorKeyConfigKey = "ApiKeys:Supervisor";

    /// <summary>Header the ERP API reads the API key from.</summary>
    private const string ApiKeyHeaderName = "X-Api-Key";

    /// <summary>
    /// Registers the typed <see cref="SupervisorErpClient"/> (base address from the configuration key
    /// <c>ErpApi:BaseUrl</c>, key from <c>ApiKeys:Supervisor</c>).
    /// </summary>
    /// <param name="services">Service collection to register into.</param>
    /// <param name="configuration">Configuration that provides <c>ErpApi:BaseUrl</c> and <c>ApiKeys:Supervisor</c>.</param>
    /// <returns>The same service collection, so calls can be chained.</returns>
    /// <remarks>
    /// The keys are read when a client is built, not when this method runs, so a host may merge further
    /// configuration sources — deployment settings, test overrides — afterwards. A host that wants the
    /// check at startup resolves <see cref="SupervisorErpClient"/> as part of its startup.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// <c>ErpApi:BaseUrl</c> is missing or not an absolute URI, or <c>ApiKeys:Supervisor</c> is missing.
    /// </exception>
    public static IServiceCollection AddSupervisorErpClient(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddHttpClient<SupervisorErpClient>(client =>
        {
            var (baseAddress, supervisorKey) = ReadErpSettings(configuration);

            client.BaseAddress = baseAddress;
            client.DefaultRequestHeaders.Add(ApiKeyHeaderName, supervisorKey);
        });

        return services;
    }

    /// <summary>
    /// Reads and validates the settings every ERP call needs. Runs when a client is built rather than at
    /// registration, so the configuration has been fully merged by then.
    /// </summary>
    /// <param name="configuration">Configuration to read from.</param>
    /// <returns>The base address (with a trailing slash) and the supervisor key.</returns>
    /// <exception cref="InvalidOperationException">
    /// <c>ErpApi:BaseUrl</c> is missing or not an absolute URI, or <c>ApiKeys:Supervisor</c> is missing.
    /// </exception>
    private static (Uri BaseAddress, string SupervisorKey) ReadErpSettings(IConfiguration configuration)
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

        // Fail fast: a missing key would otherwise surface as a 401 the first time a supervisor
        // approves a change request.
        var supervisorKey = configuration[SupervisorKeyConfigKey];

        if (string.IsNullOrWhiteSpace(supervisorKey))
        {
            throw new InvalidOperationException(
                $"Configuration value '{SupervisorKeyConfigKey}' is required but was not set. " +
                "Set it with 'dotnet user-secrets set \"ApiKeys:Supervisor\" \"<key>\"' or an environment variable.");
        }

        // Guarantee a trailing slash so relative request paths are appended to, not replaced.
        var normalized = uri.AbsoluteUri.EndsWith('/') ? uri.AbsoluteUri : $"{uri.AbsoluteUri}/";

        return (new Uri(normalized), supervisorKey);
    }
}
