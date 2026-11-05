using System.Net;
using System.Text;

namespace Agent.Tools.Tests;

public class ErpApiClientTests
{
    [Fact]
    public async Task GetOrderAsync_ReturnsDeserializedOrder()
    {
        var handler = new StubHttpMessageHandler(_ => Json("""
            {
              "id": 1023, "status": "InProduction", "orderDate": "2026-10-01",
              "requestedDeliveryDate": "2026-11-20", "productionStartDate": "2026-11-14",
              "customer": { "id": 4, "name": "Nordpack Oy", "country": "FI", "isPriority": true },
              "lines": []
            }
            """));

        var order = await CreateClient(handler).GetOrderAsync(1023);

        Assert.NotNull(order);
        Assert.Equal(1023, order.Id);
        Assert.Equal("InProduction", order.Status);
        Assert.Equal(new DateOnly(2026, 11, 14), order.ProductionStartDate);
        Assert.True(order.Customer.IsPriority);
        Assert.Empty(order.Lines);
    }

    [Fact]
    public async Task GetOrderAsync_ReturnsNull_WhenNotFound()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

        var order = await CreateClient(handler).GetOrderAsync(9999);

        Assert.Null(order);
    }

    [Fact]
    public async Task GetShipmentsAsync_DistinguishesMissingOrderFromEmptyShipmentList()
    {
        var missingOrder = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        Assert.Null(await CreateClient(missingOrder).GetShipmentsAsync(9999));

        var noShipments = new StubHttpMessageHandler(_ => Json("[]"));
        Assert.Empty((await CreateClient(noShipments).GetShipmentsAsync(1023))!);
    }

    [Fact]
    public async Task FindOrdersAsync_OmitsEmptyFilters()
    {
        var handler = new StubHttpMessageHandler(_ => Json("[]"));

        await CreateClient(handler).FindOrdersAsync(null, null);

        Assert.Equal("http://erp.test/orders", handler.Requests.Single().RequestUri!.ToString());
    }

    [Fact]
    public async Task FindOrdersAsync_EscapesFilterValues()
    {
        var handler = new StubHttpMessageHandler(_ => Json("[]"));

        await CreateClient(handler).FindOrdersAsync("Nordpack Oy", "In Production");

        var query = handler.Requests.Single().RequestUri!.Query;
        Assert.Equal("?customerName=Nordpack%20Oy&status=In%20Production", query);
    }

    [Fact]
    public async Task GetAsync_Throws_WhenErpReturnsServerError()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));

        await Assert.ThrowsAsync<HttpRequestException>(() => CreateClient(handler).GetOrderAsync(1023));
    }

    private static ErpApiClient CreateClient(StubHttpMessageHandler handler)
        => new(new HttpClient(handler) { BaseAddress = new Uri("http://erp.test/") });

    private static HttpResponseMessage Json(string body)
        => new(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
}
