namespace Agent.Tools;

/// <summary>An order with its customer and order lines, as returned by <c>GET /orders/{id}</c>.</summary>
/// <param name="Id">Order id.</param>
/// <param name="Status">Current order status name, for example <c>InProduction</c>.</param>
/// <param name="OrderDate">Date the order was placed.</param>
/// <param name="RequestedDeliveryDate">Delivery date requested by the customer.</param>
/// <param name="ProductionStartDate">Planned production start, when scheduled.</param>
/// <param name="Customer">Customer that placed the order.</param>
/// <param name="Lines">Product lines on the order.</param>
public sealed record OrderDetail(
    int Id,
    string Status,
    DateOnly OrderDate,
    DateOnly RequestedDeliveryDate,
    DateOnly? ProductionStartDate,
    Customer Customer,
    IReadOnlyList<OrderLine> Lines);

/// <summary>Customer that placed an order.</summary>
/// <param name="Id">Customer id.</param>
/// <param name="Name">Customer name.</param>
/// <param name="Country">Customer country.</param>
/// <param name="IsPriority">Whether the customer is flagged as a priority account.</param>
public sealed record Customer(int Id, string Name, string Country, bool IsPriority);

/// <summary>A single product line on an order.</summary>
/// <param name="ProductId">Product id.</param>
/// <param name="Sku">Product stock keeping unit.</param>
/// <param name="ProductName">Product name.</param>
/// <param name="Category">Product category.</param>
/// <param name="Unit">Unit of measure, for example <c>pcs</c> or <c>reel</c>.</param>
/// <param name="Quantity">Ordered quantity.</param>
/// <param name="UnitPrice">Price per unit.</param>
/// <param name="LineTotal">Ordered quantity multiplied by the unit price.</param>
public sealed record OrderLine(
    int ProductId,
    string Sku,
    string ProductName,
    string Category,
    string Unit,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal);

/// <summary>Compact order representation used in order lists, as returned by <c>GET /orders</c>.</summary>
/// <param name="Id">Order id.</param>
/// <param name="CustomerId">Id of the customer that placed the order.</param>
/// <param name="CustomerName">Name of the customer that placed the order.</param>
/// <param name="OrderDate">Date the order was placed.</param>
/// <param name="RequestedDeliveryDate">Delivery date requested by the customer.</param>
/// <param name="ProductionStartDate">Planned production start, when scheduled.</param>
/// <param name="Status">Current order status name.</param>
/// <param name="LineCount">Number of product lines on the order.</param>
public sealed record OrderSummary(
    int Id,
    int CustomerId,
    string CustomerName,
    DateOnly OrderDate,
    DateOnly RequestedDeliveryDate,
    DateOnly? ProductionStartDate,
    string Status,
    int LineCount);

/// <summary>Inventory of one product across all warehouses.</summary>
/// <param name="ProductId">Product id.</param>
/// <param name="Sku">Product stock keeping unit.</param>
/// <param name="ProductName">Product name.</param>
/// <param name="Unit">Unit of measure.</param>
/// <param name="TotalAvailable">Total available quantity across all warehouses (<c>on hand - reserved</c>).</param>
/// <param name="Warehouses">Per-warehouse stock breakdown.</param>
public sealed record ProductInventory(
    int ProductId,
    string Sku,
    string ProductName,
    string Unit,
    int TotalAvailable,
    IReadOnlyList<WarehouseStock> Warehouses);

/// <summary>Stock of one product in one warehouse.</summary>
/// <param name="WarehouseId">Warehouse id.</param>
/// <param name="WarehouseName">Warehouse name.</param>
/// <param name="City">City the warehouse is located in.</param>
/// <param name="QuantityOnHand">Units physically on hand.</param>
/// <param name="QuantityReserved">Units already reserved by orders.</param>
/// <param name="Available">Available units (<c>on hand - reserved</c>).</param>
public sealed record WarehouseStock(
    int WarehouseId,
    string WarehouseName,
    string City,
    int QuantityOnHand,
    int QuantityReserved,
    int Available);

/// <summary>A change request stored in the ERP, as returned by <c>POST /change-requests</c>.</summary>
/// <param name="Id">Change request id.</param>
/// <param name="OrderId">Order the change applies to.</param>
/// <param name="Type">Kind of change, for example <c>DeliveryDateChange</c>.</param>
/// <param name="CurrentValue">Value on the order when the request was created.</param>
/// <param name="RequestedValue">Value the order should have after approval.</param>
/// <param name="Reason">Why the change was requested.</param>
/// <param name="Status">Current status — <c>Pending</c>, <c>Approved</c> or <c>Rejected</c>.</param>
/// <param name="CreatedBy">Who raised the request.</param>
/// <param name="CreatedAt">When the request was created.</param>
/// <param name="DecidedBy">Who decided, once decided.</param>
/// <param name="DecidedAt">When it was decided, once decided.</param>
/// <param name="DecisionComment">Note added with the decision, when present.</param>
public sealed record ChangeRequest(
    int Id,
    int OrderId,
    string Type,
    string CurrentValue,
    string RequestedValue,
    string Reason,
    string Status,
    string CreatedBy,
    DateTimeOffset CreatedAt,
    string? DecidedBy,
    DateTimeOffset? DecidedAt,
    string? DecisionComment);

/// <summary>How the ERP responded to a submitted change request.</summary>
public enum ChangeRequestOutcome
{
    /// <summary>The request was stored with status <c>Pending</c> and now waits for a supervisor.</summary>
    Created,

    /// <summary>No order with the submitted id exists.</summary>
    OrderNotFound,

    /// <summary>The ERP refused the change; <see cref="ChangeRequestSubmission.Problem"/> says why.</summary>
    Rejected
}

/// <summary>Result of submitting a change request. Only the ERP can approve or reject one.</summary>
/// <param name="Outcome">What the ERP did with the submission.</param>
/// <param name="Request">The stored request when <paramref name="Outcome"/> is <see cref="ChangeRequestOutcome.Created"/>.</param>
/// <param name="Problem">The ERP's explanation when <paramref name="Outcome"/> is <see cref="ChangeRequestOutcome.Rejected"/>.</param>
public sealed record ChangeRequestSubmission(
    ChangeRequestOutcome Outcome,
    ChangeRequest? Request,
    string? Problem);

/// <summary>A shipment for an order.</summary>
/// <param name="Id">Shipment id.</param>
/// <param name="OrderId">Order the shipment belongs to.</param>
/// <param name="Carrier">Carrier handling the shipment.</param>
/// <param name="Status">Current shipment status name.</param>
/// <param name="TrackingNumber">Carrier tracking number, when assigned.</param>
/// <param name="EstimatedDeliveryDate">Estimated delivery date, when known.</param>
/// <param name="DeliveredDate">Actual delivery date, when already delivered.</param>
public sealed record Shipment(
    int Id,
    int OrderId,
    string Carrier,
    string Status,
    string? TrackingNumber,
    DateOnly? EstimatedDeliveryDate,
    DateOnly? DeliveredDate);
