using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Agent.Tools;

/// <summary>
/// Typed HTTP client for the read-only mock ERP API. Every method issues a GET and returns
/// <c>null</c> (or an empty list) when the ERP reports the resource as missing, so callers never
/// have to treat a 404 as an exception.
/// </summary>
public sealed class ErpApiClient
{
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
}
