using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agent.Tools.Tests;

public class OrderToolsTests
{
    private const string OrderJson = """
        {
          "id": 1023,
          "status": "InProduction",
          "orderDate": "2026-10-01",
          "requestedDeliveryDate": "2026-11-20",
          "productionStartDate": "2026-11-14",
          "customer": { "id": 4, "name": "Nordpack Oy", "country": "FI", "isPriority": true },
          "lines": [
            {
              "productId": 1, "sku": "PK-100", "productName": "Corrugated Box", "category": "Boxes",
              "unit": "pcs", "quantity": 10, "unitPrice": 1.5, "lineTotal": 15
            }
          ]
        }
        """;

    private const string CreatedRequestJson = """
        {
          "id": 42,
          "orderId": 1023,
          "type": "DeliveryDateChange",
          "currentValue": "2026-12-03",
          "requestedValue": "2026-12-17",
          "reason": "Customer asked for a later delivery.",
          "status": "Pending",
          "createdBy": "customer-service-agent",
          "createdAt": "2026-11-06T10:00:00+00:00",
          "decidedBy": null,
          "decidedAt": null,
          "decisionComment": null
        }
        """;

    [Fact]
    public async Task GetOrderStatus_ReturnsCompactJson_ForExistingOrder()
    {
        var handler = new StubHttpMessageHandler(_ => Json(OrderJson));
        var tools = CreateTools(handler);

        var result = await tools.GetOrderStatus(1023);

        Assert.Contains("\"status\":\"InProduction\"", result);
        Assert.Contains("\"customerName\":\"Nordpack Oy\"", result);
        Assert.Contains("\"isPriorityCustomer\":true", result);
        Assert.Contains("\"productionStartDate\":\"2026-11-14\"", result);
        Assert.Contains("\"lineCount\":1", result);
        Assert.Equal("http://erp.test/orders/1023", handler.Requests.Single().RequestUri!.ToString());
    }

    [Fact]
    public async Task GetOrderStatus_ReturnsNotFoundMessage_InsteadOfThrowing()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var tools = CreateTools(handler);

        var result = await tools.GetOrderStatus(9999);

        Assert.Equal("Order 9999 was not found in the ERP.", result);
    }

    [Fact]
    public async Task FindOrdersByCustomer_SendsFiltersAndReturnsJson()
    {
        const string ordersJson = """
            [
              {
                "id": 1001, "customerId": 4, "customerName": "Nordpack Oy",
                "orderDate": "2026-10-01", "requestedDeliveryDate": "2026-10-20",
                "productionStartDate": null, "status": "Delayed", "lineCount": 2
              }
            ]
            """;

        var handler = new StubHttpMessageHandler(_ => Json(ordersJson));
        var tools = CreateTools(handler);

        var result = await tools.FindOrdersByCustomer("Nordpack", "delayed");

        Assert.Contains("\"count\":1", result);
        Assert.Contains("\"status\":\"Delayed\"", result);
        Assert.Contains("\"id\":1001", result);

        var uri = handler.Requests.Single().RequestUri!.ToString();
        Assert.Equal("http://erp.test/orders", new Uri(uri).GetLeftPart(UriPartial.Path));
        Assert.Contains("customerName=Nordpack", uri);
        Assert.Contains("status=Delayed", uri);
    }

    [Fact]
    public async Task FindOrdersByCustomer_ReturnsNotFoundMessage_WhenNoOrdersMatch()
    {
        var handler = new StubHttpMessageHandler(_ => Json("[]"));
        var tools = CreateTools(handler);

        var result = await tools.FindOrdersByCustomer("Ghost Ltd", null);

        Assert.Equal("No orders found for customer 'Ghost Ltd'.", result);
    }

    [Fact]
    public async Task FindOrdersByCustomer_RejectsUnknownStatus_WithoutCallingErp()
    {
        var handler = new StubHttpMessageHandler(_ => Json("[]"));
        var tools = CreateTools(handler);

        var result = await tools.FindOrdersByCustomer("Nordpack", "Bogus");

        Assert.Empty(handler.Requests);
        Assert.Contains("'Bogus' is not a valid order status", result);
        Assert.Contains("Confirmed, InProduction, Shipped, Delivered, Delayed, Cancelled", result);
    }

    [Fact]
    public async Task CheckInventory_ReturnsPerWarehouseJson_ForMatchingProducts()
    {
        const string inventoryJson = """
            [
              {
                "productId": 7, "sku": "PK-200", "productName": "Corrugated Reel", "unit": "reel",
                "totalAvailable": 130,
                "warehouses": [
                  {
                    "warehouseId": 1, "warehouseName": "Imatra", "city": "Imatra",
                    "quantityOnHand": 100, "quantityReserved": 20, "available": 80
                  },
                  {
                    "warehouseId": 2, "warehouseName": "Oulu", "city": "Oulu",
                    "quantityOnHand": 60, "quantityReserved": 10, "available": 50
                  }
                ]
              }
            ]
            """;

        var handler = new StubHttpMessageHandler(_ => Json(inventoryJson));
        var tools = CreateTools(handler);

        var result = await tools.CheckInventory("Corrugated");

        Assert.Contains("\"count\":1", result);
        Assert.Contains("\"totalAvailable\":130", result);
        Assert.Contains("\"warehouseName\":\"Imatra\"", result);
        Assert.Contains("\"available\":50", result);

        var uri = handler.Requests.Single().RequestUri!.ToString();
        Assert.Equal("http://erp.test/inventory", new Uri(uri).GetLeftPart(UriPartial.Path));
        Assert.Contains("productName=Corrugated", uri);
    }

    [Fact]
    public async Task CheckInventory_ReturnsNotFoundMessage_WhenNoProductMatches()
    {
        var handler = new StubHttpMessageHandler(_ => Json("[]"));
        var tools = CreateTools(handler);

        var result = await tools.CheckInventory("Unobtainium");

        Assert.Equal("No product matching 'Unobtainium' was found in the ERP.", result);
    }

    [Fact]
    public async Task GetShipmentInfo_ReturnsShipmentJson_ForShippedOrder()
    {
        const string shipmentsJson = """
            [
              {
                "id": 55, "orderId": 1023, "carrier": "DHL", "status": "InTransit",
                "trackingNumber": "DHL-123456", "estimatedDeliveryDate": "2026-11-18",
                "deliveredDate": null
              }
            ]
            """;

        var handler = new StubHttpMessageHandler(_ => Json(shipmentsJson));
        var tools = CreateTools(handler);

        var result = await tools.GetShipmentInfo(1023);

        Assert.Contains("\"count\":1", result);
        Assert.Contains("\"carrier\":\"DHL\"", result);
        Assert.Contains("\"trackingNumber\":\"DHL-123456\"", result);
        Assert.Equal("http://erp.test/shipments/by-order/1023", handler.Requests.Single().RequestUri!.ToString());
    }

    [Fact]
    public async Task GetShipmentInfo_ReturnsNotFoundMessage_WhenOrderMissing()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var tools = CreateTools(handler);

        var result = await tools.GetShipmentInfo(9999);

        Assert.Equal("Order 9999 was not found in the ERP.", result);
    }

    [Fact]
    public async Task GetShipmentInfo_ReportsNoShipments_WhenOrderHasNone()
    {
        var handler = new StubHttpMessageHandler(_ => Json("[]"));
        var tools = CreateTools(handler);

        var result = await tools.GetShipmentInfo(1023);

        Assert.Equal("Order 1023 has no shipments yet.", result);
    }

    [Fact]
    public async Task ProposeDeliveryDateChange_CreatesPendingRequest_AndReportsIt()
    {
        var handler = new StubHttpMessageHandler(_ => Json(CreatedRequestJson, HttpStatusCode.Created));
        var tools = CreateTools(handler);

        var result = await tools.ProposeDeliveryDateChange(
            1023,
            new DateOnly(2026, 12, 17),
            "Customer asked for a later delivery.");

        Assert.Contains("\"changeRequestId\":42", result);
        Assert.Contains("\"status\":\"Pending\"", result);
        Assert.Contains("Pending supervisor approval", result);

        var request = handler.Requests.Single();
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("http://erp.test/change-requests", request.RequestUri!.ToString());
        Assert.Contains("\"type\":\"DeliveryDateChange\"", handler.LastRequestBody);
        Assert.Contains("\"requestedValue\":\"2026-12-17\"", handler.LastRequestBody);
        Assert.Contains("\"reason\":\"Customer asked for a later delivery.\"", handler.LastRequestBody);
    }

    [Fact]
    public async Task ProposeDeliveryDateChange_ExplainsTheRefusal_WhenErpRejectsTheChange()
    {
        const string problemJson = """
            {
              "title": "One or more validation errors occurred.",
              "status": 400,
              "errors": {
                "orderId": ["Order 1017 is Delivered and its delivery date can no longer be changed."]
              }
            }
            """;

        var handler = new StubHttpMessageHandler(_ => Json(problemJson, HttpStatusCode.BadRequest));
        var tools = CreateTools(handler);

        var result = await tools.ProposeDeliveryDateChange(
            1017,
            new DateOnly(2026, 12, 17),
            "Customer asked for a later delivery.");

        Assert.Contains("no change request was created", result);
        Assert.Contains("is Delivered and its delivery date can no longer be changed", result);
    }

    [Fact]
    public async Task ProposeDeliveryDateChange_ReturnsNotFoundMessage_WhenOrderMissing()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var tools = CreateTools(handler);

        var result = await tools.ProposeDeliveryDateChange(
            9999,
            new DateOnly(2026, 12, 17),
            "Customer asked for a later delivery.");

        Assert.Equal("Order 9999 was not found in the ERP.", result);
    }

    [Fact]
    public async Task ProposeDeliveryDateChange_RequiresAReason_WithoutCallingErp()
    {
        var handler = new StubHttpMessageHandler(_ => Json(CreatedRequestJson, HttpStatusCode.Created));
        var tools = CreateTools(handler);

        var result = await tools.ProposeDeliveryDateChange(1023, new DateOnly(2026, 12, 17), "   ");

        Assert.Empty(handler.Requests);
        Assert.Contains("reason is required", result);
    }

    [Fact]
    public void Tools_ExposeDescriptionsForMethodAndParameters()
    {
        var methods = typeof(OrderTools).GetMethods()
            .Where(m => m.GetCustomAttributes(typeof(System.ComponentModel.DescriptionAttribute), false).Length > 0)
            .ToList();

        Assert.Equal(5, methods.Count);

        foreach (var method in methods)
        {
            foreach (var parameter in method.GetParameters())
            {
                Assert.NotEmpty(parameter.GetCustomAttributes(
                    typeof(System.ComponentModel.DescriptionAttribute), false));
            }
        }
    }

    [Fact]
    public void Tools_ExposeNoWayToApproveRejectOrApplyAChange()
    {
        // The separation the whole design relies on: deciding on a change request is a supervisor
        // action in the ERP API, and nothing in this library may expose it to the agent.
        var suspicious = typeof(OrderTools).Assembly.GetExportedTypes()
            .SelectMany(type => type.GetMethods()
                .Where(method => !method.IsSpecialName)
                .Select(method => $"{type.Name}.{method.Name}"))
            .Where(name =>
                name.Contains("Approve", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Reject", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Decide", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Apply", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.Empty(suspicious);
    }

    private static OrderTools CreateTools(StubHttpMessageHandler handler)
        => new(
            new ErpApiClient(new HttpClient(handler) { BaseAddress = new Uri("http://erp.test/") }),
            NullLogger<OrderTools>.Instance);

    private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK)
        => new(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
}
