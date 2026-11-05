namespace Erp.Api.Domain;

public class Customer
{
    public int Id { get; set; }

    public required string Name { get; set; }

    public required string Country { get; set; }

    public bool IsPriority { get; set; }

    public ICollection<Order> Orders { get; set; } = [];
}
