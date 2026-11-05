using Microsoft.AspNetCore.Authentication;

namespace Erp.Api.Authentication;

/// <summary>Registration helpers for the API-key authentication scheme and the policies built on it.</summary>
public static class AuthenticationExtensions
{
    /// <summary>
    /// Registers the <c>ApiKey</c> scheme and the change request policies. The keys themselves are
    /// read from configuration (<c>ApiKeys:Agent</c> and <c>ApiKeys:Supervisor</c>) and are expected
    /// to come from user-secrets or environment variables, never from appsettings.json.
    /// </summary>
    /// <param name="services">Service collection to register into.</param>
    /// <returns>The same service collection, so calls can be chained.</returns>
    public static IServiceCollection AddApiKeyAuthentication(this IServiceCollection services)
    {
        services
            .AddAuthentication(ApiKeyDefaults.Scheme)
            .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(ApiKeyDefaults.Scheme, null);

        services.AddAuthorization(options =>
        {
            // The agent and supervisors can both read change requests.
            options.AddPolicy(AuthorizationPolicies.ChangeRequestRead, policy => policy
                .AddAuthenticationSchemes(ApiKeyDefaults.Scheme)
                .RequireAuthenticatedUser()
                .RequireRole(ApiKeyDefaults.AgentRole, ApiKeyDefaults.SupervisorRole));

            // Only the agent may propose a change.
            options.AddPolicy(AuthorizationPolicies.ChangeRequestCreate, policy => policy
                .AddAuthenticationSchemes(ApiKeyDefaults.Scheme)
                .RequireAuthenticatedUser()
                .RequireRole(ApiKeyDefaults.AgentRole));

            // Only a human supervisor may approve or reject.
            options.AddPolicy(AuthorizationPolicies.ChangeRequestDecide, policy => policy
                .AddAuthenticationSchemes(ApiKeyDefaults.Scheme)
                .RequireAuthenticatedUser()
                .RequireRole(ApiKeyDefaults.SupervisorRole));
        });

        return services;
    }
}
