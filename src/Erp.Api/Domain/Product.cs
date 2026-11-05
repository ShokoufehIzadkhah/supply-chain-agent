namespace Erp.Api.Domain;

public class Product
{
    public int Id { get; set; }

    public required string Sku { get; set; }

    public required string Name { get; set; }

    public required string Category { get; set; }

    public required string Unit { get; set; }

    public ICollection<OrderLine> OrderLines { get; set; } = [];

    public ICollection<Inventory> Inventory { get; set; } = [];
}
