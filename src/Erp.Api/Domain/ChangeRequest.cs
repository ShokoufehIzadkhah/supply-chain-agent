namespace Erp.Api.Domain;

/// <summary>
/// A proposed change to an order (for example a new requested delivery date) that a human
/// supervisor must approve or reject. The agent can only create requests; approving one is what
/// actually applies the change to the order.
/// </summary>
public class ChangeRequest
{
    public int Id { get; set; }

    public int OrderId { get; set; }

    public ChangeRequestType Type { get; set; }

    /// <summary>Value on the order at the moment the request was created (an ISO date for a date change).</summary>
    public required string CurrentValue { get; set; }

    /// <summary>Value the requester wants the order to have (an ISO date for a date change).</summary>
    public required string RequestedValue { get; set; }

    /// <summary>Why the change is being requested.</summary>
    public required string Reason { get; set; }

    public ChangeRequestStatus Status { get; set; } = ChangeRequestStatus.Pending;

    /// <summary>Who raised the request — typically the agent service account.</summary>
    public required string CreatedBy { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public string? DecidedBy { get; set; }

    public DateTimeOffset? DecidedAt { get; set; }

    public string? DecisionComment { get; set; }

    public Order Order { get; set; } = null!;
}
