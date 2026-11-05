using System.Net;
using System.Net.Http.Json;
using Erp.Api.Domain;
using Erp.Api.Features.Orders;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Erp.Api.Tests;

public class OrdersEndpointTests : IClassFixture<ErpApiFactory>
{
    private readonly ErpApiFactory _factory;
    private readonly HttpClient _client;

    public OrdersEndpointTests(ErpApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetOrderById_ReturnsOrderWithLines_ForExistingOrder()
    {
        var response = await _client.GetAsync("/orders/1023");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var order = (await response.Content.ReadFromJsonAsync<OrderDetailDto>(TestJson.Options))!;

        Assert.Equal(1023, order.Id);
        Assert.Equal(OrderStatus.InProduction, order.Status);

        // Order 1023 is the pinned demo order: a priority customer, production starting 8 days out.
        Assert.True(order.Customer.IsPriority);
        Assert.False(string.IsNullOrWhiteSpace(order.Customer.Name));
        Assert.Equal(DateOnly.FromDateTime(DateTime.Today).AddDays(8), order.ProductionStartDate);

        // Seeded orders carry between one and four lines, each with product details and a total.
        Assert.InRange(order.Lines.Count, 1, 4);
        Assert.All(order.Lines, line =>
        {
            Assert.False(string.IsNullOrWhiteSpace(line.Sku));
            Assert.False(string.IsNullOrWhiteSpace(line.ProductName));
            Assert.True(line.Quantity > 0);
            Assert.True(line.UnitPrice > 0);
            Assert.Equal(line.Quantity * line.UnitPrice, line.LineTotal);
        });
    }

    [Fact]
    public async Task GetOrderById_Returns404ProblemDetails_ForMissingOrder()
    {
        var response = await _client.GetAsync("/orders/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var problem = (await response.Content.ReadFromJsonAsync<ProblemDetails>(TestJson.Options))!;

        Assert.Equal(404, problem.Status);
        Assert.Equal("Order not found", problem.Title);
    }

    [Fact]
    public async Task GetOrders_FiltersByStatus()
    {
        var response = await _client.GetAsync("/orders?status=Delayed");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var orders = (await response.Content.ReadFromJsonAsync<List<OrderSummaryDto>>(TestJson.Options))!;

        Assert.Equal(6, orders.Count);
        Assert.All(orders, order =>
        {
            Assert.Equal(OrderStatus.Delayed, order.Status);
            Assert.False(string.IsNullOrWhiteSpace(order.CustomerName));
            Assert.InRange(order.LineCount, 1, 4);
        });

        // The endpoint must return exactly the delayed orders stored in the seeded database.
        var expectedIds = await _factory.QueryAsync(db => db.Orders
            .Where(o => o.Status == OrderStatus.Delayed)
            .OrderBy(o => o.Id)
            .Select(o => o.Id)
            .ToListAsync());

        Assert.Equal(expectedIds, orders.Select(o => o.Id));
    }
}
