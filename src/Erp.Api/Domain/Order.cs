namespace Erp.Api.Domain;

/// <summary>
/// A customer order. Ids are issued by the ERP starting at 1001.
/// </summary>
public class Order
{
    public int Id { get; set; }

    public int CustomerId { get; set; }

    public DateOnly OrderDate { get; set; }

    public DateOnly RequestedDeliveryDate { get; set; }

    public DateOnly? ProductionStartDate { get; set; }

    public OrderStatus Status { get; set; } = OrderStatus.Confirmed;

    public Customer Customer { get; set; } = null!;

    public ICollection<OrderLine> Lines { get; set; } = [];

    public ICollection<Shipment> Shipments { get; set; } = [];
}
