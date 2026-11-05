using System.Net;
using System.Net.Http.Json;
using Erp.Api.Features.Inventory;
using Microsoft.EntityFrameworkCore;

namespace Erp.Api.Tests;

public class InventoryEndpointTests : IClassFixture<ErpApiFactory>
{
    private readonly ErpApiFactory _factory;
    private readonly HttpClient _client;

    public InventoryEndpointTests(ErpApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetInventoryByProduct_TotalAvailableIsOnHandMinusReserved()
    {
        var response = await _client.GetAsync("/inventory/1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var inventory = (await response.Content.ReadFromJsonAsync<ProductInventoryDto>(TestJson.Options))!;

        Assert.Equal(1, inventory.ProductId);
        Assert.False(string.IsNullOrWhiteSpace(inventory.Sku));

        // Every product is stocked in all three warehouses.
        Assert.Equal(3, inventory.Warehouses.Count);

        // Available per warehouse is on hand minus reserved, and the total is the sum of those.
        Assert.All(inventory.Warehouses, w => Assert.Equal(w.QuantityOnHand - w.QuantityReserved, w.Available));
        Assert.Equal(inventory.Warehouses.Sum(w => w.QuantityOnHand - w.QuantityReserved), inventory.TotalAvailable);

        // The same must hold for the rows the seeder wrote to the database.
        var stored = await _factory.QueryAsync(db => db.Inventory
            .Where(i => i.ProductId == 1)
            .Select(i => new { i.QuantityOnHand, i.QuantityReserved })
            .ToListAsync());

        Assert.Equal(3, stored.Count);
        Assert.Equal(stored.Sum(i => i.QuantityOnHand - i.QuantityReserved), inventory.TotalAvailable);
    }
}
