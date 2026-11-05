using System.Net;
using System.Net.Http.Json;
using Erp.Api.Domain;
using Erp.Api.Features.Shipments;
using Microsoft.EntityFrameworkCore;

namespace Erp.Api.Tests;

public class ShipmentsEndpointTests : IClassFixture<ErpApiFactory>
{
    private readonly ErpApiFactory _factory;
    private readonly HttpClient _client;

    public ShipmentsEndpointTests(ErpApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetShipmentsByOrder_ReturnsShipments_ForOrderThatHasThem()
    {
        var orderId = await _factory.QueryAsync(db => db.Orders
            .Where(o => o.Shipments.Any())
            .OrderBy(o => o.Id)
            .Select(o => o.Id)
            .FirstAsync());

        var response = await _client.GetAsync($"/shipments/by-order/{orderId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var shipments = (await response.Content.ReadFromJsonAsync<List<ShipmentDto>>(TestJson.Options))!;

        Assert.NotEmpty(shipments);
        Assert.All(shipments, shipment =>
        {
            Assert.Equal(orderId, shipment.OrderId);
            Assert.False(string.IsNullOrWhiteSpace(shipment.Carrier));
            Assert.False(string.IsNullOrWhiteSpace(shipment.TrackingNumber));
            // Only shipped, delivered or delayed orders get a shipment, never a pending one.
            Assert.NotEqual(ShipmentStatus.Pending, shipment.Status);
            Assert.NotNull(shipment.EstimatedDeliveryDate);
        });
    }

    [Fact]
    public async Task GetShipmentsByOrder_ReturnsEmptyList_ForOrderWithoutShipments()
    {
        // Order 1023 is still in production, so no shipment exists for it yet.
        var response = await _client.GetAsync("/shipments/by-order/1023");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty((await response.Content.ReadFromJsonAsync<List<ShipmentDto>>(TestJson.Options))!);
    }

    [Fact]
    public async Task GetShipmentsByOrder_Returns404_ForMissingOrder()
    {
        var response = await _client.GetAsync("/shipments/by-order/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
