using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Agent.Tools;

/// <summary>
/// Agent tools that answer order, inventory and shipment questions by calling the mock ERP API, and
/// propose delivery date changes that a human supervisor must approve. Every tool returns a compact,
/// LLM-friendly <see cref="string"/> — short JSON on success, or a clear human-readable message when
/// nothing matches. Missing records and refused changes never throw. No tool can approve, reject or
/// apply a change.
/// </summary>
public sealed class OrderTools
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly string[] KnownOrderStatuses =
        ["Confirmed", "InProduction", "Shipped", "Delivered", "Delayed", "Cancelled"];

    /// <summary>Identity recorded on change requests this agent proposes.</summary>
    private const string AgentIdentity = "customer-service-agent";

    /// <summary>Message returned after a change request was created and is waiting for a supervisor.</summary>
    private const string PendingApprovalMessage = "Pending supervisor approval";

    private readonly ErpApiClient _erp;
    private readonly ILogger<OrderTools> _logger;

    /// <summary>Creates the tools.</summary>
    /// <param name="erp">Typed client for the mock ERP API.</param>
    /// <param name="logger">Logger used to record every tool call.</param>
    public OrderTools(ErpApiClient erp, ILogger<OrderTools> logger)
    {
        _erp = erp;
        _logger = logger;
    }

    /// <summary>Gets the current status of one order, together with its key dates and customer.</summary>
    /// <param name="orderId">Numeric ERP order id, for example <c>1023</c>.</param>
    /// <returns>Short JSON with the status, or a message when the order does not exist.</returns>
    [Description(
        "Returns the current status and key dates of a single customer order, identified by its numeric " +
        "order id. Use it whenever the user asks about one specific order, for example 'what is the " +
        "status of order 1023?' or 'when will order 1023 go into production?'. The result is a short " +
        "JSON object with the order id, status, customer name, whether the customer is a priority " +
        "account, the order date, the requested delivery date, the production start date (when " +
        "scheduled) and the number of order lines. If no order with that id exists, a plain-text " +
        "'not found' message is returned instead of an error.")]
    public async Task<string> GetOrderStatus(
        [Description(
            "The numeric ERP order id to look up, for example 1023. Must be the integer order id, " +
            "not a customer name or a tracking number.")]
        int orderId)
    {
        return await RunAsync("GetOrderStatus", new { orderId }, async () =>
        {
            var order = await _erp.GetOrderAsync(orderId);
            if (order is null)
            {
                return Refused($"Order {orderId} was not found in the ERP.");
            }

            var result = new OrderStatusResult(
                order.Id,
                order.Status,
                order.Customer.Name,
                order.Customer.IsPriority,
                order.OrderDate,
                order.RequestedDeliveryDate,
                order.ProductionStartDate,
                order.Lines.Count);

            return Found(result);
        });
    }

    /// <summary>Finds the orders of a customer, optionally limited to one status.</summary>
    /// <param name="customerName">Customer name or a distinctive part of it.</param>
    /// <param name="status">Optional exact order status filter.</param>
    /// <returns>Short JSON list of matching orders, or a message when nothing matches.</returns>
    [Description(
        "Finds every order belonging to a customer, matched on the customer name (case-insensitive, " +
        "partial names allowed) and optionally filtered by order status. Use it for questions such as " +
        "'which orders does Nordpack have?', 'show me the delayed orders of Nordpack' or 'how many open " +
        "orders does this customer have?'. The result is a short JSON object with the requested customer, " +
        "the status filter that was applied and a list of matching orders (id, status, order date, " +
        "requested delivery date, production start date and line count). An empty list is returned when " +
        "the customer has no matching orders.")]
    public async Task<string> FindOrdersByCustomer(
        [Description(
            "The customer name, or a distinctive part of it, for example 'Nordpack'. The match is " +
            "case-insensitive and substring-based, so 'nord' also works. Do not pass an order id here.")]
        string customerName,
        [Description(
            "Optional order status to filter by. Must be exactly one of: Confirmed, InProduction, " +
            "Shipped, Delivered, Delayed, Cancelled (case-insensitive). Pass null to return orders in " +
            "every status.")]
        string? status)
    {
        return await RunAsync("FindOrdersByCustomer", new { customerName, status }, async () =>
        {
            if (!TryNormalizeStatus(status, out var normalizedStatus, out var error))
            {
                return Refused(error!);
            }

            var orders = await _erp.FindOrdersAsync(customerName, normalizedStatus);
            if (orders.Count == 0)
            {
                var suffix = normalizedStatus is null ? string.Empty : $" with status {normalizedStatus}";
                return Refused($"No orders found for customer '{customerName}'{suffix}.");
            }

            var result = new CustomerOrdersResult(customerName, normalizedStatus, orders.Count, orders);
            return Found(result);
        });
    }

    /// <summary>Checks how much stock is available for products matching a name.</summary>
    /// <param name="productName">Product name or a distinctive part of it.</param>
    /// <returns>Short JSON with per-warehouse and total availability, or a message when nothing matches.</returns>
    [Description(
        "Checks available stock for every product whose name matches the supplied text " +
        "(case-insensitive, partial names allowed). Use it for questions such as 'how many do we have " +
        "of X?' or 'is product X available in Imatra?'. The result is a short JSON object listing each " +
        "matching product with its sku, unit, total available quantity (on hand minus reserved) and a " +
        "per-warehouse breakdown of available, on-hand and reserved quantities. If no product matches, a " +
        "plain-text 'not found' message is returned instead of an error.")]
    public async Task<string> CheckInventory(
        [Description(
            "The product name, or a distinctive part of it, for example 'Corrugated'. The match is " +
            "case-insensitive and substring-based. Do not pass a product id or an order id.")]
        string productName)
    {
        return await RunAsync("CheckInventory", new { productName }, async () =>
        {
            var products = await _erp.FindInventoryAsync(productName);
            if (products.Count == 0)
            {
                return Refused($"No product matching '{productName}' was found in the ERP.");
            }

            var result = new InventoryResult(productName, products.Count, products);
            return Found(result);
        });
    }

    /// <summary>Gets the shipments of an order.</summary>
    /// <param name="orderId">Numeric ERP order id, for example <c>1023</c>.</param>
    /// <returns>Short JSON list of shipments, or a message when the order is missing or unshipped.</returns>
    [Description(
        "Returns the shipment details of a single order, identified by its numeric order id: carrier, " +
        "shipment status, tracking number and estimated or actual delivery date. Use it for questions " +
        "such as 'has order 1023 shipped?', 'who is the carrier?' or 'when will it arrive?'. The result " +
        "is a short JSON object listing the shipments of the order. If the order does not exist, or it " +
        "exists but has no shipments yet, a plain-text message is returned instead of an error.")]
    public async Task<string> GetShipmentInfo(
        [Description(
            "The numeric ERP order id whose shipments should be looked up, for example 1023. Must be the " +
            "integer order id, not a tracking number.")]
        int orderId)
    {
        return await RunAsync("GetShipmentInfo", new { orderId }, async () =>
        {
            var shipments = await _erp.GetShipmentsAsync(orderId);
            if (shipments is null)
            {
                return Refused($"Order {orderId} was not found in the ERP.");
            }

            if (shipments.Count == 0)
            {
                return Refused($"Order {orderId} has no shipments yet.");
            }

            var result = new ShipmentInfoResult(orderId, shipments.Count, shipments);
            return Found(result);
        });
    }

    /// <summary>
    /// Proposes a new delivery date for an order by creating a pending change request. The tool only
    /// asks for the change; a human supervisor approves or rejects it, and this library cannot do
    /// either.
    /// </summary>
    /// <param name="orderId">Numeric ERP order id, for example <c>1023</c>.</param>
    /// <param name="newDate">Delivery date the order should have after a supervisor approves.</param>
    /// <param name="reason">Why the change is needed, for example what the customer asked for.</param>
    /// <returns>Short JSON with the new request id, or an explanation of why no request was created.</returns>
    [Description(
        "Proposes a new delivery date for an order and creates a PENDING change request that a human " +
        "supervisor still has to approve. Nothing about the order changes when this tool runs. Use it " +
        "only when the user explicitly asks to move, postpone or bring forward a delivery date, and " +
        "only after checking the relevant policy: if the change breaks a policy, explain why instead " +
        "of proposing it. The result is a short JSON object with the change request id, the order id, " +
        "the requested delivery date, the status 'Pending' and the message 'Pending supervisor " +
        "approval'. If the request cannot be created — the order does not exist, or the ERP refuses " +
        "the change because of a hard rule such as the order already being delivered or cancelled, or " +
        "the new date being before the order date — a plain-text explanation is returned instead of an " +
        "error, and no request is created. You never approve, reject or apply a change yourself.")]
    public async Task<string> ProposeDeliveryDateChange(
        [Description(
            "The numeric ERP order id whose delivery date should change, for example 1023. Must be the " +
            "integer order id, not a customer name.")]
        int orderId,
        [Description(
            "The new delivery date the customer wants, as an ISO date such as 2026-12-17. Must be on or " +
            "after the order date.")]
        DateOnly newDate,
        [Description(
            "Why the delivery date should change, for example 'customer asked to postpone by two weeks'. " +
            "The reason is stored on the change request so the supervisor can judge it.")]
        string reason)
    {
        return await RunAsync("ProposeDeliveryDateChange", new { orderId, newDate, reason }, async () =>
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                return Refused("A reason is required to propose a delivery date change.");
            }

            var submission = await _erp.CreateChangeRequestAsync(orderId, newDate, reason.Trim(), AgentIdentity);

            switch (submission.Outcome)
            {
                case ChangeRequestOutcome.OrderNotFound:
                    return Refused($"Order {orderId} was not found in the ERP.");

                case ChangeRequestOutcome.Rejected:
                    return Refused(
                        $"The ERP refused the delivery date change for order {orderId}, so no change " +
                        $"request was created: {submission.Problem ?? "the change breaks an ERP rule"}");

                default:
                    var request = submission.Request!;
                    return Found(new ChangeRequestResult(
                        request.Id,
                        request.OrderId,
                        request.RequestedValue,
                        request.Status,
                        PendingApprovalMessage));
            }
        });
    }

    /// <summary>
    /// Runs a tool body, timing it and logging the tool name, arguments, duration and whether a result
    /// was found. Failures are logged as errors and rethrown.
    /// </summary>
    private async Task<string> RunAsync(string tool, object arguments, Func<Task<ToolOutcome>> call)
    {
        var started = Stopwatch.GetTimestamp();

        try
        {
            var outcome = await call();

            _logger.LogInformation(
                "Tool {ToolName} called with {ToolArguments} in {DurationMs} ms, success: {ToolSuccess}",
                tool,
                arguments,
                Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                outcome.Success);

            return outcome.Output;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Tool {ToolName} called with {ToolArguments} failed after {DurationMs} ms",
                tool,
                arguments,
                Stopwatch.GetElapsedTime(started).TotalMilliseconds);

            throw;
        }
    }

    private static ToolOutcome Found(object value)
        => new(JsonSerializer.Serialize(value, JsonOptions), Success: true);

    private static ToolOutcome Refused(string message) => new(message, Success: false);

    private static bool TryNormalizeStatus(string? status, out string? normalized, out string? error)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            normalized = null;
            error = null;
            return true;
        }

        normalized = KnownOrderStatuses.FirstOrDefault(
            s => string.Equals(s, status.Trim(), StringComparison.OrdinalIgnoreCase));

        if (normalized is null)
        {
            error = $"'{status}' is not a valid order status. Valid values are: " +
                    $"{string.Join(", ", KnownOrderStatuses)}.";
            return false;
        }

        error = null;
        return true;
    }

    private readonly record struct ToolOutcome(string Output, bool Success);
}

/// <summary>Result of <see cref="OrderTools.GetOrderStatus"/>.</summary>
/// <param name="OrderId">Order id.</param>
/// <param name="Status">Current order status.</param>
/// <param name="CustomerName">Customer that placed the order.</param>
/// <param name="IsPriorityCustomer">Whether the customer is a priority account.</param>
/// <param name="OrderDate">Date the order was placed.</param>
/// <param name="RequestedDeliveryDate">Delivery date requested by the customer.</param>
/// <param name="ProductionStartDate">Planned production start, when scheduled.</param>
/// <param name="LineCount">Number of product lines on the order.</param>
public sealed record OrderStatusResult(
    int OrderId,
    string Status,
    string CustomerName,
    bool IsPriorityCustomer,
    DateOnly OrderDate,
    DateOnly RequestedDeliveryDate,
    DateOnly? ProductionStartDate,
    int LineCount);

/// <summary>Result of <see cref="OrderTools.FindOrdersByCustomer"/>.</summary>
/// <param name="CustomerName">Customer name that was searched for.</param>
/// <param name="Status">Status filter that was applied, or <c>null</c> for all statuses.</param>
/// <param name="Count">Number of matching orders.</param>
/// <param name="Orders">Matching orders.</param>
public sealed record CustomerOrdersResult(
    string CustomerName,
    string? Status,
    int Count,
    IReadOnlyList<OrderSummary> Orders);

/// <summary>Result of <see cref="OrderTools.CheckInventory"/>.</summary>
/// <param name="ProductName">Product name that was searched for.</param>
/// <param name="Count">Number of matching products.</param>
/// <param name="Products">Matching products with their per-warehouse stock.</param>
public sealed record InventoryResult(
    string ProductName,
    int Count,
    IReadOnlyList<ProductInventory> Products);

/// <summary>Result of <see cref="OrderTools.ProposeDeliveryDateChange"/>.</summary>
/// <param name="ChangeRequestId">Id of the pending change request that was created.</param>
/// <param name="OrderId">Order the change applies to.</param>
/// <param name="RequestedDeliveryDate">Delivery date the order should have after approval.</param>
/// <param name="Status">Status of the new request, which is always <c>Pending</c>.</param>
/// <param name="Message">Reminder that a supervisor still has to decide.</param>
public sealed record ChangeRequestResult(
    int ChangeRequestId,
    int OrderId,
    string RequestedDeliveryDate,
    string Status,
    string Message);

/// <summary>Result of <see cref="OrderTools.GetShipmentInfo"/>.</summary>
/// <param name="OrderId">Order id.</param>
/// <param name="Count">Number of shipments.</param>
/// <param name="Shipments">Shipments of the order.</param>
public sealed record ShipmentInfoResult(
    int OrderId,
    int Count,
    IReadOnlyList<Shipment> Shipments);
