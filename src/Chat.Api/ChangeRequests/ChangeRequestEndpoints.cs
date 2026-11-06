namespace Chat.Api.ChangeRequests;

/// <summary>
/// Passes the change request endpoints of the ERP API through to the chat UI, so the browser has a single
/// origin to talk to. These are the supervisor's endpoints and they carry the supervisor key; the agent's
/// own tools cannot reach them.
/// </summary>
/// <remarks>
/// CORS restricts which pages a browser may call this API from. It is not authentication, so in this
/// local demo the endpoints below are effectively open to anyone who can reach the API. A deployment
/// would put a signed-in supervisor (Microsoft Entra ID) in front of them and require the Supervisor
/// role before calling the ERP API.
/// </remarks>
public static class ChangeRequestEndpoints
{
    /// <summary>Maps the proxied <c>/change-requests</c> endpoints.</summary>
    /// <param name="app">Endpoint route builder.</param>
    /// <returns>The same builder, so calls can be chained.</returns>
    public static IEndpointRouteBuilder MapChangeRequestEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/change-requests").WithTags("Change requests");

        group.MapGet("", GetChangeRequestsAsync)
            .WithName("ProxyGetChangeRequests")
            .WithSummary("Lists change requests, newest first, optionally filtered by status.");

        group.MapPost("/{id:int}/approve", ApproveAsync)
            .WithName("ProxyApproveChangeRequest")
            .WithSummary("Approves a pending change request as the supervisor.");

        group.MapPost("/{id:int}/reject", RejectAsync)
            .WithName("ProxyRejectChangeRequest")
            .WithSummary("Rejects a pending change request as the supervisor.");

        return app;
    }

    private static async Task<IResult> GetChangeRequestsAsync(
        SupervisorErpClient erp,
        string? status,
        CancellationToken cancellationToken)
        => Forward(await erp.GetChangeRequestsAsync(status, cancellationToken));

    private static async Task<IResult> ApproveAsync(
        int id,
        DecisionRequest? decision,
        SupervisorErpClient erp,
        CancellationToken cancellationToken)
        => Forward(await erp.ApproveAsync(id, decision, cancellationToken));

    private static async Task<IResult> RejectAsync(
        int id,
        DecisionRequest? decision,
        SupervisorErpClient erp,
        CancellationToken cancellationToken)
        => Forward(await erp.RejectAsync(id, decision, cancellationToken));

    // The status code and the body are passed through as the ERP API wrote them, so validation problems
    // and the "already decided" conflict reach the UI exactly as the API expressed them.
    private static IResult Forward(ErpResponse response)
        => Results.Text(
            response.Body,
            response.ContentType ?? "application/json",
            statusCode: (int)response.StatusCode);
}
