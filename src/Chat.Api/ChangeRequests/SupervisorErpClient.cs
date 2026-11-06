using System.Net;
using System.Net.Http.Json;

namespace Chat.Api.ChangeRequests;

/// <summary>One exchange with the ERP API, kept as the ERP API answered it.</summary>
/// <param name="StatusCode">Status the ERP API returned.</param>
/// <param name="Body">Response body as text, so problem details survive the hop unchanged.</param>
/// <param name="ContentType">Content type of the body, for example <c>application/problem+json</c>.</param>
public sealed record ErpResponse(HttpStatusCode StatusCode, string Body, string? ContentType);

/// <summary>
/// Calls the change request endpoints of the ERP API with the supervisor key. It lives here rather than
/// in <c>Agent.Tools</c> on purpose: the library the agent runs from still contains no code path that can
/// approve or reject a change request, so only a human supervisor — who is the only one with this key —
/// can decide one.
/// </summary>
/// <param name="http">Client configured with the ERP API base address and the supervisor key.</param>
public sealed class SupervisorErpClient(HttpClient http)
{
    /// <summary>Lists change requests, newest first.</summary>
    /// <param name="status">Optional status filter, for example <c>Pending</c>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The ERP API's response, unmodified.</returns>
    public Task<ErpResponse> GetChangeRequestsAsync(string? status, CancellationToken cancellationToken)
    {
        var path = string.IsNullOrWhiteSpace(status)
            ? "change-requests"
            : $"change-requests?status={Uri.EscapeDataString(status)}";

        return SendAsync(new HttpRequestMessage(HttpMethod.Get, path), cancellationToken);
    }

    /// <summary>Approves a pending change request, which applies it to the order.</summary>
    /// <param name="id">Change request id.</param>
    /// <param name="decision">Who decided and an optional comment.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The ERP API's response, unmodified.</returns>
    public Task<ErpResponse> ApproveAsync(int id, DecisionRequest? decision, CancellationToken cancellationToken)
        => DecideAsync(id, "approve", decision, cancellationToken);

    /// <summary>Rejects a pending change request, leaving the order untouched.</summary>
    /// <param name="id">Change request id.</param>
    /// <param name="decision">Who decided and an optional comment.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The ERP API's response, unmodified.</returns>
    public Task<ErpResponse> RejectAsync(int id, DecisionRequest? decision, CancellationToken cancellationToken)
        => DecideAsync(id, "reject", decision, cancellationToken);

    private Task<ErpResponse> DecideAsync(
        int id,
        string action,
        DecisionRequest? decision,
        CancellationToken cancellationToken)
        => SendAsync(
            new HttpRequestMessage(HttpMethod.Post, $"change-requests/{id}/{action}")
            {
                // The ERP API requires a decision body, so an absent one becomes an empty decision,
                // which the API fills in with the supervisor role.
                Content = JsonContent.Create(decision ?? new DecisionRequest(null, null)),
            },
            cancellationToken);

    private async Task<ErpResponse> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var response = await http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        return new ErpResponse(
            response.StatusCode,
            body,
            response.Content.Headers.ContentType?.ToString());
    }
}

/// <summary>A supervisor's decision on a change request, as the ERP API expects it.</summary>
/// <param name="DecidedBy">Name of the person deciding; the ERP API uses the supervisor role when omitted.</param>
/// <param name="DecisionComment">Optional note explaining the decision.</param>
public sealed record DecisionRequest(string? DecidedBy, string? DecisionComment);
