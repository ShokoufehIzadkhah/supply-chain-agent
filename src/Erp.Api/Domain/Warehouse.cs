namespace Erp.Api.Domain;

public class Warehouse
{
    public int Id { get; set; }

    public required string Name { get; set; }

    public required string City { get; set; }

    public ICollection<Inventory> Inventory { get; set; } = [];
}
