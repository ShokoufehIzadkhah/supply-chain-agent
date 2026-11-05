namespace Erp.Api.Domain;

/// <summary>Lifecycle of a change request. Only a human supervisor can move it out of <see cref="Pending"/>.</summary>
public enum ChangeRequestStatus
{
    Pending,
    Approved,
    Rejected
}
