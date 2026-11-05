namespace Erp.Api.Domain;

/// <summary>
/// Stock held for a product in a warehouse. Keyed by the product/warehouse pair.
/// </summary>
public class Inventory
{
    public int ProductId { get; set; }

    public int WarehouseId { get; set; }

    public int QuantityOnHand { get; set; }

    public int QuantityReserved { get; set; }

    public Product Product { get; set; } = null!;

    public Warehouse Warehouse { get; set; } = null!;
}
