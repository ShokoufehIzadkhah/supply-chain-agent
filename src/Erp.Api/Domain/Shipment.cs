namespace Erp.Api.Domain;

public class Shipment
{
    public int Id { get; set; }

    public int OrderId { get; set; }

    public required string Carrier { get; set; }

    public ShipmentStatus Status { get; set; } = ShipmentStatus.Pending;

    public string? TrackingNumber { get; set; }

    public DateOnly? EstimatedDeliveryDate { get; set; }

    public DateOnly? DeliveredDate { get; set; }

    public Order Order { get; set; } = null!;
}
