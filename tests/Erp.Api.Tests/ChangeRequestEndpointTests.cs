using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Erp.Api.Domain;
using Erp.Api.Features.ChangeRequests;
using Erp.Api.Features.Orders;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Erp.Api.Tests;

public class ChangeRequestEndpointTests : IClassFixture<ErpApiFactory>
{
    private const string DateFormat = "yyyy-MM-dd";
    private const string HeaderName = "X-Api-Key";

    private readonly ErpApiFactory _factory;

    public ChangeRequestEndpointTests(ErpApiFactory factory) => _factory = factory;

    // --- create -----------------------------------------------------------------------------

    [Fact]
    public async Task Create_WithAgentKey_Returns201PendingRequest()
    {
        using var client = CreateClient(ErpApiFactory.AgentApiKey);
        var order = await GetOrderAsync(1023);
        var newDate = order.RequestedDeliveryDate.AddDays(14);

        using var response = await client.PostAsJsonAsync("/change-requests", NewRequest(1023, newDate), TestJson.Options);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = (await response.Content.ReadFromJsonAsync<ChangeRequestDto>(TestJson.Options))!;

        Assert.True(created.Id > 0);
        Assert.Equal(1023, created.OrderId);
        Assert.Equal(ChangeRequestType.DeliveryDateChange, created.Type);
        Assert.Equal(ChangeRequestStatus.Pending, created.Status);
        Assert.Equal(Format(order.RequestedDeliveryDate), created.CurrentValue);
        Assert.Equal(Format(newDate), created.RequestedValue);
        Assert.Equal("agent", created.CreatedBy);
        Assert.False(string.IsNullOrWhiteSpace(created.Reason));
        Assert.Null(created.DecidedBy);
        Assert.Null(created.DecidedAt);
        Assert.Equal($"/change-requests/{created.Id}", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task Create_ForUnknownOrder_Returns404()
    {
        using var client = CreateClient(ErpApiFactory.AgentApiKey);

        using var response = await client.PostAsJsonAsync(
            "/change-requests",
            NewRequest(999999, DateOnly.FromDateTime(DateTime.Today).AddDays(10)),
            TestJson.Options);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var problem = (await response.Content.ReadFromJsonAsync<ProblemDetails>(TestJson.Options))!;
        Assert.Equal(404, problem.Status);
        Assert.Equal("Order not found", problem.Title);
    }

    [Fact]
    public async Task Create_WithNonDateRequestedValue_Returns400()
    {
        using var client = CreateClient(ErpApiFactory.AgentApiKey);
        var request = new CreateChangeRequestRequest(
            1023,
            ChangeRequestType.DeliveryDateChange,
            "not-a-date",
            "Customer asked for a later delivery.",
            "agent");

        using var response = await client.PostAsJsonAsync("/change-requests", request, TestJson.Options);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithoutKey_Returns401()
    {
        using var client = CreateClient(apiKey: null);

        using var response = await client.PostAsJsonAsync(
            "/change-requests",
            NewRequest(1023, DateOnly.FromDateTime(DateTime.Today).AddDays(10)),
            TestJson.Options);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithSupervisorKey_Returns403()
    {
        // Only the agent proposes changes; a supervisor key is authenticated but not allowed here.
        using var client = CreateClient(ErpApiFactory.SupervisorApiKey);

        using var response = await client.PostAsJsonAsync(
            "/change-requests",
            NewRequest(1023, DateOnly.FromDateTime(DateTime.Today).AddDays(10)),
            TestJson.Options);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // --- read -------------------------------------------------------------------------------

    [Fact]
    public async Task GetById_ReturnsTheRequest()
    {
        var created = await CreateChangeRequestAsync();

        using var client = CreateClient(ErpApiFactory.AgentApiKey);
        var fetched = await client.GetFromJsonAsync<ChangeRequestDto>($"/change-requests/{created.Id}", TestJson.Options);

        Assert.NotNull(fetched);
        Assert.Equal(created.Id, fetched!.Id);
        Assert.Equal(created.RequestedValue, fetched.RequestedValue);
    }

    [Fact]
    public async Task GetList_WithSupervisorKey_FiltersByStatus()
    {
        // The approvals UI uses the supervisor key to list pending requests, so it must be allowed here.
        var created = await CreateChangeRequestAsync();

        using var client = CreateClient(ErpApiFactory.SupervisorApiKey);

        var pending = await client.GetFromJsonAsync<List<ChangeRequestDto>>(
            "/change-requests?status=Pending",
            TestJson.Options);

        Assert.NotNull(pending);
        Assert.Contains(pending!, request => request.Id == created.Id);
        Assert.All(pending!, request => Assert.Equal(ChangeRequestStatus.Pending, request.Status));

        var rejected = await client.GetFromJsonAsync<List<ChangeRequestDto>>(
            "/change-requests?status=Rejected",
            TestJson.Options);

        Assert.NotNull(rejected);
        Assert.DoesNotContain(rejected!, request => request.Id == created.Id);
    }

    [Fact]
    public async Task GetById_Returns404_ForUnknownRequest()
    {
        using var client = CreateClient(ErpApiFactory.AgentApiKey);

        using var response = await client.GetAsync("/change-requests/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var problem = (await response.Content.ReadFromJsonAsync<ProblemDetails>(TestJson.Options))!;
        Assert.Equal("Change request not found", problem.Title);
    }

    // --- approve / reject -------------------------------------------------------------------

    [Fact]
    public async Task Approve_WithSupervisorKey_UpdatesOrderDeliveryDateAndMarksApproved()
    {
        var order = await GetOrderAsync(1023);
        var newDate = order.RequestedDeliveryDate.AddDays(30);
        var created = await CreateChangeRequestAsync(1023, newDate);

        using var client = CreateClient(ErpApiFactory.SupervisorApiKey);
        using var response = await client.PostAsJsonAsync(
            $"/change-requests/{created.Id}/approve",
            new DecisionRequest("Sanna Supervisor", "Customer confirmed the new date."),
            TestJson.Options);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var decided = (await response.Content.ReadFromJsonAsync<ChangeRequestDto>(TestJson.Options))!;
        Assert.Equal(ChangeRequestStatus.Approved, decided.Status);
        Assert.Equal("Sanna Supervisor", decided.DecidedBy);
        Assert.NotNull(decided.DecidedAt);

        // The change must be visible through the public orders endpoint...
        var updated = await GetOrderAsync(1023);
        Assert.Equal(newDate, updated.RequestedDeliveryDate);

        // ...and persisted to the order row itself.
        var stored = await _factory.QueryAsync(db => db.Orders
            .Where(o => o.Id == 1023)
            .Select(o => o.RequestedDeliveryDate)
            .FirstAsync());

        Assert.Equal(newDate, stored);
    }

    [Fact]
    public async Task Approve_WithAgentKey_Returns403()
    {
        var created = await CreateChangeRequestAsync();

        using var client = CreateClient(ErpApiFactory.AgentApiKey);
        using var response = await client.PostAsJsonAsync(
            $"/change-requests/{created.Id}/approve",
            new DecisionRequest("Agent", null),
            TestJson.Options);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Approve_WithoutKey_Returns401()
    {
        var created = await CreateChangeRequestAsync();

        using var client = CreateClient(apiKey: null);
        using var response = await client.PostAsJsonAsync(
            $"/change-requests/{created.Id}/approve",
            new DecisionRequest("Agent", null),
            TestJson.Options);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Approve_WhenAlreadyDecided_Returns409()
    {
        var created = await CreateChangeRequestAsync();

        using var client = CreateClient(ErpApiFactory.SupervisorApiKey);
        using var first = await client.PostAsJsonAsync(
            $"/change-requests/{created.Id}/approve",
            new DecisionRequest("Sanna Supervisor", null),
            TestJson.Options);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        using var second = await client.PostAsJsonAsync(
            $"/change-requests/{created.Id}/approve",
            new DecisionRequest("Sanna Supervisor", null),
            TestJson.Options);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Reject_WithSupervisorKey_LeavesTheOrderUnchanged()
    {
        var order = await GetOrderAsync(1023);
        var created = await CreateChangeRequestAsync(1023, order.RequestedDeliveryDate.AddDays(60));

        using var client = CreateClient(ErpApiFactory.SupervisorApiKey);
        using var response = await client.PostAsJsonAsync(
            $"/change-requests/{created.Id}/reject",
            new DecisionRequest("Sanna Supervisor", "Production cannot move."),
            TestJson.Options);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var decided = (await response.Content.ReadFromJsonAsync<ChangeRequestDto>(TestJson.Options))!;
        Assert.Equal(ChangeRequestStatus.Rejected, decided.Status);
        Assert.Equal("Production cannot move.", decided.DecisionComment);

        var unchanged = await GetOrderAsync(1023);
        Assert.Equal(order.RequestedDeliveryDate, unchanged.RequestedDeliveryDate);
    }

    // --- helpers ----------------------------------------------------------------------------

    private HttpClient CreateClient(string? apiKey)
    {
        var client = _factory.CreateClient();

        if (apiKey is not null)
        {
            client.DefaultRequestHeaders.Add(HeaderName, apiKey);
        }

        return client;
    }

    private async Task<OrderDetailDto> GetOrderAsync(int orderId)
    {
        using var client = _factory.CreateClient();
        var order = await client.GetFromJsonAsync<OrderDetailDto>($"/orders/{orderId}", TestJson.Options);
        return order!;
    }

    private async Task<ChangeRequestDto> CreateChangeRequestAsync(int orderId = 1023, DateOnly? requestedDeliveryDate = null)
    {
        var order = await GetOrderAsync(orderId);
        var target = requestedDeliveryDate ?? order.RequestedDeliveryDate.AddDays(21);

        using var client = CreateClient(ErpApiFactory.AgentApiKey);
        using var response = await client.PostAsJsonAsync("/change-requests", NewRequest(orderId, target), TestJson.Options);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<ChangeRequestDto>(TestJson.Options))!;
    }

    private static CreateChangeRequestRequest NewRequest(int orderId, DateOnly requestedDeliveryDate) =>
        new(
            orderId,
            ChangeRequestType.DeliveryDateChange,
            Format(requestedDeliveryDate),
            "Customer asked for a later delivery.",
            "agent");

    private static string Format(DateOnly date) => date.ToString(DateFormat, CultureInfo.InvariantCulture);
}
