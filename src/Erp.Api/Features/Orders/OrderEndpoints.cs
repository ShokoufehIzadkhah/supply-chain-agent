using Erp.Api.Data;
using Erp.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Erp.Api.Features.Orders;

/// <summary>
/// Read-only order endpoints used by the assistant to answer questions about customer orders.
/// </summary>
public static class OrderEndpoints
{
    /// <summary>Maps the <c>/orders</c> read endpoints.</summary>
    public static IEndpointRouteBuilder MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/orders").WithTags("Orders");

        group.MapGet("/{id:int}", GetOrderById)
            .WithName("GetOrderById")
            .Produces<OrderDetailDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("", GetOrders)
            .WithName("GetOrders")
            .Produces<IReadOnlyList<OrderSummaryDto>>();

        return app;
    }

    /// <summary>Returns a single order together with its customer and order lines.</summary>
    /// <param name="id">Order id, for example <c>1023</c>.</param>
    /// <param name="db">Database context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">The order exists and is returned.</response>
    /// <response code="404">No order with the given id exists.</response>
    private static async Task<IResult> GetOrderById(int id, ErpDbContext db, CancellationToken ct)
    {
        // Project straight to an anonymous shape (never the entity) and materialise the
        // enum in memory so the response can render the status name.
        var order = await db.Orders
            .AsNoTracking()
            .Where(o => o.Id == id)
            .Select(o => new
            {
                o.Id,
                o.Status,
                o.OrderDate,
                o.RequestedDeliveryDate,
                o.ProductionStartDate,
                Customer = new
                {
                    o.Customer.Id,
                    o.Customer.Name,
                    o.Customer.Country,
                    o.Customer.IsPriority
                },
                Lines = o.Lines
                    .OrderBy(l => l.Product.Sku)
                    .Select(l => new
                    {
                        l.ProductId,
                        l.Product.Sku,
                        ProductName = l.Product.Name,
                        l.Product.Category,
                        l.Product.Unit,
                        l.Quantity,
                        l.UnitPrice
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync(ct);

        if (order is null)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Order not found",
                detail: $"No order with id {id} exists.");
        }

        var dto = new OrderDetailDto(
            order.Id,
            order.Status,
            order.OrderDate,
            order.RequestedDeliveryDate,
            order.ProductionStartDate,
            new CustomerDto(
                order.Customer.Id,
                order.Customer.Name,
                order.Customer.Country,
                order.Customer.IsPriority),
            order.Lines
                .Select(l => new OrderLineDto(
                    l.ProductId,
                    l.Sku,
                    l.ProductName,
                    l.Category,
                    l.Unit,
                    l.Quantity,
                    l.UnitPrice,
                    l.Quantity * l.UnitPrice))
                .ToList());

        return Results.Ok(dto);
    }

    /// <summary>Returns orders, optionally filtered by customer and/or status.</summary>
    /// <remarks>All filters are optional and combine with AND. An empty result is returned as an empty list.</remarks>
    /// <param name="db">Database context.</param>
    /// <param name="customerId">Optional exact customer id.</param>
    /// <param name="customerName">Optional case-insensitive substring of the customer name.</param>
    /// <param name="status">Optional order status.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Zero or more matching orders.</response>
    private static async Task<IResult> GetOrders(
        ErpDbContext db,
        int? customerId,
        string? customerName,
        OrderStatus? status,
        CancellationToken ct)
    {
        var query = db.Orders.AsNoTracking();

        if (customerId is not null)
        {
            query = query.Where(o => o.CustomerId == customerId);
        }

        if (!string.IsNullOrWhiteSpace(customerName))
        {
            var needle = customerName.Trim().ToLower();
            query = query.Where(o => o.Customer.Name.ToLower().Contains(needle));
        }

        if (status is not null)
        {
            query = query.Where(o => o.Status == status);
        }

        var orders = await query
            .OrderBy(o => o.Id)
            .Select(o => new OrderSummaryDto(
                o.Id,
                o.CustomerId,
                o.Customer.Name,
                o.OrderDate,
                o.RequestedDeliveryDate,
                o.ProductionStartDate,
                o.Status,
                o.Lines.Count))
            .ToListAsync(ct);

        return Results.Ok(orders);
    }
}

/// <summary>Compact order representation used in order lists.</summary>
/// <param name="Id">Order id.</param>
/// <param name="CustomerId">Id of the customer that placed the order.</param>
/// <param name="CustomerName">Name of the customer that placed the order.</param>
/// <param name="OrderDate">Date the order was placed.</param>
/// <param name="RequestedDeliveryDate">Delivery date requested by the customer.</param>
/// <param name="ProductionStartDate">Planned start of production, when scheduled.</param>
/// <param name="Status">Current order status.</param>
/// <param name="LineCount">Number of product lines on the order.</param>
public sealed record OrderSummaryDto(
    int Id,
    int CustomerId,
    string CustomerName,
    DateOnly OrderDate,
    DateOnly RequestedDeliveryDate,
    DateOnly? ProductionStartDate,
    OrderStatus Status,
    int LineCount);

/// <summary>Full order details, including the customer and every order line.</summary>
/// <param name="Id">Order id.</param>
/// <param name="Status">Current order status.</param>
/// <param name="OrderDate">Date the order was placed.</param>
/// <param name="RequestedDeliveryDate">Delivery date requested by the customer.</param>
/// <param name="ProductionStartDate">Planned start of production, when scheduled.</param>
/// <param name="Customer">Customer that placed the order.</param>
/// <param name="Lines">Product lines on the order.</param>
public sealed record OrderDetailDto(
    int Id,
    OrderStatus Status,
    DateOnly OrderDate,
    DateOnly RequestedDeliveryDate,
    DateOnly? ProductionStartDate,
    CustomerDto Customer,
    IReadOnlyList<OrderLineDto> Lines);

/// <summary>Customer that placed an order.</summary>
/// <param name="Id">Customer id.</param>
/// <param name="Name">Customer name.</param>
/// <param name="Country">Customer country.</param>
/// <param name="IsPriority">Whether the customer is flagged as a priority account.</param>
public sealed record CustomerDto(int Id, string Name, string Country, bool IsPriority);

/// <summary>A single product line on an order.</summary>
/// <param name="ProductId">Product id.</param>
/// <param name="Sku">Product stock keeping unit.</param>
/// <param name="ProductName">Product name.</param>
/// <param name="Category">Product category.</param>
/// <param name="Unit">Unit of measure (for example <c>pcs</c> or <c>reel</c>).</param>
/// <param name="Quantity">Ordered quantity.</param>
/// <param name="UnitPrice">Price per unit.</param>
/// <param name="LineTotal">Ordered quantity multiplied by the unit price.</param>
public sealed record OrderLineDto(
    int ProductId,
    string Sku,
    string ProductName,
    string Category,
    string Unit,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal);
