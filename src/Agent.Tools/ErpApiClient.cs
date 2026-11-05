using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Agent.Tools;

/// <summary>
/// Typed HTTP client for the mock ERP API. Read methods issue a GET and return <c>null</c> (or an
/// empty list) when the ERP reports the resource as missing, so callers never have to treat a 404 as
/// an exception. The only write method submits a <em>pending</em> change request: deciding one is a
/// supervisor action that this library deliberately cannot perform.
/// </summary>
public sealed class ErpApiClient
{
    /// <summary>Wire format for the date carried by a delivery date change.</summary>
    private const string DateFormat = "yyyy-MM-dd";

    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private readonly HttpClient _http;

    /// <summary>Creates the client. The <see cref="HttpClient.BaseAddress"/> is configured by <c>AddAgentTools</c>.</summary>
    /// <param name="http">HTTP client pointed at the ERP API base address.</param>
    public ErpApiClient(HttpClient http) => _http = http;

    /// <summary>Returns the order with the given id, or <c>null</c> when no such order exists.</summary>
    /// <param name="orderId">Order id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<OrderDetail?> GetOrderAsync(int orderId, CancellationToken cancellationToken = default)
        => GetAsync<OrderDetail>($"orders/{orderId}", cancellationToken);

    /// <summary>Returns orders matching the optional filters; an empty list when nothing matches.</summary>
    /// <param name="customerName">Case-insensitive substring of the customer name, or <c>null</c> to skip.</param>
    /// <param name="status">Exact order status name, or <c>null</c> to skip.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<IReadOnlyList<OrderSummary>> FindOrdersAsync(
        string? customerName,
        string? status,
        CancellationToken cancellationToken = default)
    {
        var query = new List<string>();

        if (!string.IsNullOrWhiteSpace(customerName))
        {
            query.Add($"customerName={Uri.EscapeDataString(customerName.Trim())}");
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query.Add($"status={Uri.EscapeDataString(status.Trim())}");
        }

        var url = query.Count == 0 ? "orders" : $"orders?{string.Join('&', query)}";
        return await GetAsync<List<OrderSummary>>(url, cancellationToken) ?? [];
    }

    /// <summary>Returns the stock of every product whose name contains the supplied text.</summary>
    /// <param name="productName">Case-insensitive substring of the product name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<IReadOnlyList<ProductInventory>> FindInventoryAsync(
        string productName,
        CancellationToken cancellationToken = default)
    {
        var url = $"inventory?productName={Uri.EscapeDataString(productName.Trim())}";
        return await GetAsync<List<ProductInventory>>(url, cancellationToken) ?? [];
    }

    /// <summary>Returns the shipments of an order, or <c>null</c> when the order itself does not exist.</summary>
    /// <param name="orderId">Order id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<IReadOnlyList<Shipment>?> GetShipmentsAsync(
        int orderId,
        CancellationToken cancellationToken = default)
    {
        var shipments = await GetAsync<List<Shipment>>($"shipments/by-order/{orderId}", cancellationToken);
        return shipments;
    }

    /// <summary>
    /// Submits a <em>pending</em> delivery date change for an order. The request is created with the
    /// agent's credentials and nothing else happens: only a supervisor can approve or reject it, and
    /// this library exposes no way to do either.
    /// </summary>
    /// <param name="orderId">Order the change applies to.</param>
    /// <param name="requestedDeliveryDate">Delivery date the order should have after approval.</param>
    /// <param name="reason">Why the change is being requested.</param>
    /// <param name="createdBy">Who raised the request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// The created request, or the reason the ERP refused to create one. A refusal (an unknown order, or
    /// a change that breaks an ERP rule such as moving the date of a delivered order) is returned as
    /// data rather than thrown, because the agent is expected to explain it to the user.
    /// </returns>
    public async Task<ChangeRequestSubmission> CreateChangeRequestAsync(
        int orderId,
        DateOnly requestedDeliveryDate,
        string reason,
        string createdBy,
        CancellationToken cancellationToken = default)
    {
        var body = new
        {
            orderId,
            type = "DeliveryDateChange",
            requestedValue = requestedDeliveryDate.ToString(DateFormat, CultureInfo.InvariantCulture),
            reason,
            createdBy
        };

        using var response = await _http.PostAsJsonAsync("change-requests", body, JsonOptions, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return new ChangeRequestSubmission(ChangeRequestOutcome.OrderNotFound, null, null);
        }

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var problem = await ReadProblemAsync(response, cancellationToken);
            return new ChangeRequestSubmission(ChangeRequestOutcome.Rejected, null, problem);
        }

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var created = await JsonSerializer.DeserializeAsync<ChangeRequest>(stream, JsonOptions, cancellationToken);

        return new ChangeRequestSubmission(ChangeRequestOutcome.Created, created, null);
    }

    private async Task<T?> GetAsync<T>(string url, CancellationToken cancellationToken)
    {
        using var response = await _http.GetAsync(url, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return default;
        }

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken);
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    /// <summary>Turns an ERP validation problem response into a single sentence the agent can relay.</summary>
    private static async Task<string?> ReadProblemAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var problem = await JsonSerializer.DeserializeAsync<ValidationProblemResponse>(stream, JsonOptions, cancellationToken);

            var messages = problem?.Errors?.Values.SelectMany(messages => messages).ToList();

            if (messages is { Count: > 0 })
            {
                return string.Join(" ", messages);
            }

            return problem?.Detail ?? problem?.Title;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>The subset of an ASP.NET Core problem-details payload this client reads.</summary>
    private sealed record ValidationProblemResponse(
        string? Title,
        string? Detail,
        Dictionary<string, string[]>? Errors);
}
