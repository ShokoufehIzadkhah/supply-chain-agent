namespace Erp.Api.Authentication;

/// <summary>Authorization policy names used by the change request endpoints.</summary>
public static class AuthorizationPolicies
{
    /// <summary>Listing and reading change requests — the agent and supervisors both need this.</summary>
    public const string ChangeRequestRead = "ChangeRequestRead";

    /// <summary>Creating a change request — the agent only.</summary>
    public const string ChangeRequestCreate = "ChangeRequestCreate";

    /// <summary>Approving or rejecting a change request — supervisors only.</summary>
    public const string ChangeRequestDecide = "ChangeRequestDecide";
}
