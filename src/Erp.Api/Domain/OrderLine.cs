namespace Erp.Api.Domain;

/// <summary>
/// A single product line on an order. Keyed by the order/product pair.
/// </summary>
public class OrderLine
{
    public int OrderId { get; set; }

    public int ProductId { get; set; }

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public Order Order { get; set; } = null!;

    public Product Product { get; set; } = null!;
}
