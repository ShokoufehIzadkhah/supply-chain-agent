using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Erp.Api.Authentication;

/// <summary>
/// Authenticates callers with a shared API key sent in the <c>X-Api-Key</c> header. A single
/// scheme recognises both keys and turns them into a role (<c>Agent</c> or <c>Supervisor</c>),
/// which is what lets the endpoints tell the two apart: an agent key is authenticated but fails
/// the supervisor policy, so it gets 403 rather than 401.
/// </summary>
public sealed class ApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly IConfiguration _configuration;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IConfiguration configuration)
        : base(options, logger, encoder)
    {
        _configuration = configuration;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(ApiKeyDefaults.HeaderName, out var headerValues))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var providedKey = headerValues.ToString().Trim();
        var role = ResolveRole(providedKey);

        if (role is null)
        {
            return Task.FromResult(AuthenticateResult.Fail("Unknown API key."));
        }

        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, role), new Claim(ClaimTypes.Role, role)],
            ApiKeyDefaults.Scheme);

        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), ApiKeyDefaults.Scheme)));
    }

    /// <summary>Maps an incoming key to the role it grants, or <c>null</c> when it is not a known key.</summary>
    private string? ResolveRole(string providedKey)
    {
        if (string.IsNullOrEmpty(providedKey))
        {
            return null;
        }

        if (Matches(ApiKeyDefaults.AgentKeyConfigKey, providedKey))
        {
            return ApiKeyDefaults.AgentRole;
        }

        if (Matches(ApiKeyDefaults.SupervisorKeyConfigKey, providedKey))
        {
            return ApiKeyDefaults.SupervisorRole;
        }

        return null;
    }

    /// <summary>
    /// Compares the provided key with the configured one in constant time so a wrong key cannot be
    /// discovered by measuring response times.
    /// </summary>
    private bool Matches(string configKey, string providedKey)
    {
        var configuredKey = _configuration[configKey];

        if (string.IsNullOrEmpty(configuredKey))
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(configuredKey),
            Encoding.UTF8.GetBytes(providedKey));
    }
}
