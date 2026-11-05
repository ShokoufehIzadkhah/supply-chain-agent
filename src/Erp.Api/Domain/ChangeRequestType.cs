namespace Erp.Api.Domain;

/// <summary>
/// The kind of order change a request proposes. New kinds (for example a quantity change) are
/// added here and handled where the request is created and applied.
/// </summary>
public enum ChangeRequestType
{
    /// <summary>Move the order's requested delivery date to a new date.</summary>
    DeliveryDateChange
}
