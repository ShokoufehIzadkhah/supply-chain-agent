using Erp.Api.Data;
using Erp.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Erp.Api.Features.Shipments;

/// <summary>
/// Read-only shipment endpoints: carriers, tracking numbers and delivery dates per order.
/// </summary>
public static class ShipmentEndpoints
{
    /// <summary>Maps the <c>/shipments</c> read endpoints.</summary>
    public static IEndpointRouteBuilder MapShipmentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/shipments").WithTags("Shipments");

        group.MapGet("/by-order/{orderId:int}", GetShipmentsByOrder)
            .WithName("GetShipmentsByOrder")
            .Produces<IReadOnlyList<ShipmentDto>>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    /// <summary>Returns the shipments that belong to an order.</summary>
    /// <param name="orderId">Order id.</param>
    /// <param name="db">Database context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">The order exists; zero or more shipments are returned.</response>
    /// <response code="404">No order with the given id exists.</response>
    private static async Task<IResult> GetShipmentsByOrder(int orderId, ErpDbContext db, CancellationToken ct)
    {
        var orderExists = await db.Orders
            .AsNoTracking()
            .AnyAsync(o => o.Id == orderId, ct);

        if (!orderExists)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Order not found",
                detail: $"No order with id {orderId} exists.");
        }

        var shipments = await db.Shipments
            .AsNoTracking()
            .Where(s => s.OrderId == orderId)
            .OrderBy(s => s.Id)
            .Select(s => new ShipmentDto(
                s.Id,
                s.OrderId,
                s.Carrier,
                s.Status,
                s.TrackingNumber,
                s.EstimatedDeliveryDate,
                s.DeliveredDate))
            .ToListAsync(ct);

        return Results.Ok(shipments);
    }
}

/// <summary>A shipment for an order.</summary>
/// <param name="Id">Shipment id.</param>
/// <param name="OrderId">Order the shipment belongs to.</param>
/// <param name="Carrier">Carrier handling the shipment.</param>
/// <param name="Status">Current shipment status.</param>
/// <param name="TrackingNumber">Carrier tracking number, when assigned.</param>
/// <param name="EstimatedDeliveryDate">Estimated delivery date, when known.</param>
/// <param name="DeliveredDate">Actual delivery date, when already delivered.</param>
public sealed record ShipmentDto(
    int Id,
    int OrderId,
    string Carrier,
    ShipmentStatus Status,
    string? TrackingNumber,
    DateOnly? EstimatedDeliveryDate,
    DateOnly? DeliveredDate);
