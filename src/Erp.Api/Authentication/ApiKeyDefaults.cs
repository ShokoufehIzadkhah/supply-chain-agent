namespace Erp.Api.Authentication;

/// <summary>Names shared by the API-key authentication scheme, the policies and the configuration.</summary>
public static class ApiKeyDefaults
{
    /// <summary>Name of the single authentication scheme used by every secured endpoint.</summary>
    public const string Scheme = "ApiKey";

    /// <summary>Request header that carries the key.</summary>
    public const string HeaderName = "X-Api-Key";

    /// <summary>Role granted to the agent service account (create and read change requests).</summary>
    public const string AgentRole = "Agent";

    /// <summary>Role granted to a human supervisor (read and decide on change requests).</summary>
    public const string SupervisorRole = "Supervisor";

    /// <summary>Configuration key holding the agent's key. Supply the value through user-secrets or environment variables.</summary>
    public const string AgentKeyConfigKey = "ApiKeys:Agent";

    /// <summary>Configuration key holding the supervisor's key. Supply the value through user-secrets or environment variables.</summary>
    public const string SupervisorKeyConfigKey = "ApiKeys:Supervisor";
}
