using System.Globalization;
using System.Linq.Expressions;
using Erp.Api.Authentication;
using Erp.Api.Data;
using Erp.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Erp.Api.Features.ChangeRequests;

/// <summary>
/// Change request endpoints. The agent proposes a change and a human supervisor approves or rejects
/// it; approving a <see cref="ChangeRequestType.DeliveryDateChange"/> is what moves the order's
/// requested delivery date.
/// </summary>
public static class ChangeRequestEndpoints
{
    /// <summary>Date format used for the current and requested values of a delivery date change.</summary>
    private const string DateFormat = "yyyy-MM-dd";

    private static readonly Expression<Func<ChangeRequest, ChangeRequestDto>> ToDto = changeRequest =>
        new ChangeRequestDto(
            changeRequest.Id,
            changeRequest.OrderId,
            changeRequest.Type,
            changeRequest.CurrentValue,
            changeRequest.RequestedValue,
            changeRequest.Reason,
            changeRequest.Status,
            changeRequest.CreatedBy,
            changeRequest.CreatedAt,
            changeRequest.DecidedBy,
            changeRequest.DecidedAt,
            changeRequest.DecisionComment);

    /// <summary>Maps the <c>/change-requests</c> endpoints.</summary>
    public static IEndpointRouteBuilder MapChangeRequestEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/change-requests").WithTags("Change requests");

        group.MapPost("", CreateChangeRequest)
            .WithName("CreateChangeRequest")
            .RequireAuthorization(AuthorizationPolicies.ChangeRequestCreate)
            .Produces<ChangeRequestDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("", GetChangeRequests)
            .WithName("GetChangeRequests")
            .RequireAuthorization(AuthorizationPolicies.ChangeRequestRead)
            .Produces<IReadOnlyList<ChangeRequestDto>>();

        group.MapGet("/{id:int}", GetChangeRequestById)
            .WithName("GetChangeRequestById")
            .RequireAuthorization(AuthorizationPolicies.ChangeRequestRead)
            .Produces<ChangeRequestDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:int}/approve", ApproveChangeRequest)
            .WithName("ApproveChangeRequest")
            .RequireAuthorization(AuthorizationPolicies.ChangeRequestDecide)
            .Produces<ChangeRequestDto>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:int}/reject", RejectChangeRequest)
            .WithName("RejectChangeRequest")
            .RequireAuthorization(AuthorizationPolicies.ChangeRequestDecide)
            .Produces<ChangeRequestDto>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }

    /// <summary>Creates a pending change request for an order. The current value is read from the order.</summary>
    /// <param name="request">The change the agent proposes.</param>
    /// <param name="db">Database context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="201">The request was stored with status <c>Pending</c>.</response>
    /// <response code="400">The change type or the requested value is not valid.</response>
    /// <response code="404">No order with the given id exists.</response>
    private static async Task<IResult> CreateChangeRequest(
        CreateChangeRequestRequest request,
        ErpDbContext db,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return Invalid("reason", "A reason for the change is required.");
        }

        if (string.IsNullOrWhiteSpace(request.CreatedBy))
        {
            return Invalid("createdBy", "The requester (createdBy) is required.");
        }

        var requestedValue = request.RequestedValue?.Trim();

        if (string.IsNullOrWhiteSpace(requestedValue))
        {
            return Invalid("requestedValue", "A requested value is required.");
        }

        // Each change type decides what a valid requested value looks like.
        DateOnly? requestedDate = null;

        if (request.Type == ChangeRequestType.DeliveryDateChange)
        {
            if (!DateOnly.TryParseExact(requestedValue, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            {
                return Invalid("requestedValue", $"A delivery date change needs a date in the format {DateFormat}.");
            }

            requestedDate = parsed;
        }

        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == request.OrderId, ct);

        if (order is null)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Order not found",
                detail: $"No order with id {request.OrderId} exists.");
        }

        // Hard rules the ERP always enforces, independent of any business policy document. Softer
        // policies are left to the agent and to the human supervisor who decides on the request.
        if (order.Status is OrderStatus.Delivered or OrderStatus.Cancelled)
        {
            return Invalid(
                "orderId",
                $"Order {order.Id} is {order.Status} and its delivery date can no longer be changed.");
        }

        if (requestedDate is not null && requestedDate < order.OrderDate)
        {
            return Invalid(
                "requestedValue",
                $"The requested delivery date cannot be earlier than the order date " +
                $"({order.OrderDate.ToString(DateFormat, CultureInfo.InvariantCulture)}).");
        }

        var changeRequest = new ChangeRequest
        {
            OrderId = order.Id,
            Type = request.Type,
            CurrentValue = CurrentValueFor(request.Type, order),
            RequestedValue = requestedValue,
            Reason = request.Reason.Trim(),
            Status = ChangeRequestStatus.Pending,
            CreatedBy = request.CreatedBy.Trim(),
            CreatedAt = DateTimeOffset.UtcNow
        };

        db.ChangeRequests.Add(changeRequest);
        await db.SaveChangesAsync(ct);

        var dto = await ProjectAsync(db, changeRequest.Id, ct);

        return Results.Created($"/change-requests/{changeRequest.Id}", dto);
    }

    /// <summary>Lists change requests, optionally filtered by status.</summary>
    /// <param name="db">Database context.</param>
    /// <param name="status">Optional status filter.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Zero or more change requests, newest first.</response>
    private static async Task<IResult> GetChangeRequests(
        ErpDbContext db,
        ChangeRequestStatus? status,
        CancellationToken ct)
    {
        var query = db.ChangeRequests.AsNoTracking();

        if (status is not null)
        {
            query = query.Where(c => c.Status == status);
        }

        // Order by id: the identity column is monotonic, so descending id is "newest first".
        // (SQLite cannot order by a DateTimeOffset, which is what CreatedAt is.)
        var requests = await query
            .OrderByDescending(c => c.Id)
            .Select(ToDto)
            .ToListAsync(ct);

        return Results.Ok(requests);
    }

    /// <summary>Returns one change request.</summary>
    /// <param name="id">Change request id.</param>
    /// <param name="db">Database context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">The request exists and is returned.</response>
    /// <response code="404">No change request with the given id exists.</response>
    private static async Task<IResult> GetChangeRequestById(int id, ErpDbContext db, CancellationToken ct)
    {
        var dto = await db.ChangeRequests
            .AsNoTracking()
            .Where(c => c.Id == id)
            .Select(ToDto)
            .FirstOrDefaultAsync(ct);

        return dto is null ? NotFound(id) : Results.Ok(dto);
    }

    /// <summary>Approves a pending change request and applies it to the order.</summary>
    /// <param name="id">Change request id.</param>
    /// <param name="decision">Who decided and an optional comment.</param>
    /// <param name="db">Database context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">The request was approved and the order was updated.</response>
    /// <response code="404">No change request with the given id exists.</response>
    /// <response code="409">The request has already been approved or rejected.</response>
    private static Task<IResult> ApproveChangeRequest(
        int id,
        DecisionRequest decision,
        ErpDbContext db,
        CancellationToken ct) =>
        DecideAsync(id, decision, approve: true, db, ct);

    /// <summary>Rejects a pending change request. The order is left untouched.</summary>
    /// <param name="id">Change request id.</param>
    /// <param name="decision">Who decided and an optional comment.</param>
    /// <param name="db">Database context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">The request was rejected.</response>
    /// <response code="404">No change request with the given id exists.</response>
    /// <response code="409">The request has already been approved or rejected.</response>
    private static Task<IResult> RejectChangeRequest(
        int id,
        DecisionRequest decision,
        ErpDbContext db,
        CancellationToken ct) =>
        DecideAsync(id, decision, approve: false, db, ct);

    private static async Task<IResult> DecideAsync(
        int id,
        DecisionRequest decision,
        bool approve,
        ErpDbContext db,
        CancellationToken ct)
    {
        // The decision and the order update (for an approved date change) must land together, so
        // both are written inside one explicit transaction.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var changeRequest = await db.ChangeRequests
            .Include(c => c.Order)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (changeRequest is null)
        {
            return NotFound(id);
        }

        if (changeRequest.Status != ChangeRequestStatus.Pending)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Change request already decided",
                detail: $"Change request {id} has already been {changeRequest.Status}.");
        }

        changeRequest.Status = approve ? ChangeRequestStatus.Approved : ChangeRequestStatus.Rejected;
        changeRequest.DecidedBy = string.IsNullOrWhiteSpace(decision.DecidedBy)
            ? ApiKeyDefaults.SupervisorRole
            : decision.DecidedBy.Trim();
        changeRequest.DecidedAt = DateTimeOffset.UtcNow;
        changeRequest.DecisionComment = string.IsNullOrWhiteSpace(decision.DecisionComment)
            ? null
            : decision.DecisionComment.Trim();

        if (approve)
        {
            ApplyToOrder(changeRequest);
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        var dto = await ProjectAsync(db, changeRequest.Id, ct);

        return Results.Ok(dto);
    }

    /// <summary>Reads the value a change type replaces on the order.</summary>
    private static string CurrentValueFor(ChangeRequestType type, Order order) => type switch
    {
        ChangeRequestType.DeliveryDateChange =>
            order.RequestedDeliveryDate.ToString(DateFormat, CultureInfo.InvariantCulture),
        _ => throw new InvalidOperationException($"Change request type '{type}' is not supported.")
    };

    /// <summary>Writes an approved change onto the order.</summary>
    private static void ApplyToOrder(ChangeRequest changeRequest)
    {
        switch (changeRequest.Type)
        {
            case ChangeRequestType.DeliveryDateChange:
                changeRequest.Order.RequestedDeliveryDate = DateOnly.ParseExact(
                    changeRequest.RequestedValue,
                    DateFormat,
                    CultureInfo.InvariantCulture);
                break;
            default:
                throw new InvalidOperationException(
                    $"Change request type '{changeRequest.Type}' cannot be applied.");
        }
    }

    private static Task<ChangeRequestDto> ProjectAsync(ErpDbContext db, int id, CancellationToken ct) =>
        db.ChangeRequests
            .AsNoTracking()
            .Where(c => c.Id == id)
            .Select(ToDto)
            .FirstAsync(ct);

    private static IResult NotFound(int id) => Results.Problem(
        statusCode: StatusCodes.Status404NotFound,
        title: "Change request not found",
        detail: $"No change request with id {id} exists.");

    private static IResult Invalid(string field, string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] });
}

/// <summary>A change the agent proposes for an order.</summary>
/// <param name="OrderId">Order the change applies to.</param>
/// <param name="Type">Kind of change, for example <c>DeliveryDateChange</c>.</param>
/// <param name="RequestedValue">Value the order should have after approval (an ISO date for a date change).</param>
/// <param name="Reason">Why the change is being requested.</param>
/// <param name="CreatedBy">Who raised the request — typically the agent service account.</param>
public sealed record CreateChangeRequestRequest(
    int OrderId,
    ChangeRequestType Type,
    string RequestedValue,
    string Reason,
    string CreatedBy);

/// <summary>A supervisor's decision on a change request.</summary>
/// <param name="DecidedBy">Name of the person deciding; defaults to the supervisor role when omitted.</param>
/// <param name="DecisionComment">Optional note explaining the decision.</param>
public sealed record DecisionRequest(string? DecidedBy, string? DecisionComment);

/// <summary>A change request as returned by the API.</summary>
/// <param name="Id">Change request id.</param>
/// <param name="OrderId">Order the change applies to.</param>
/// <param name="Type">Kind of change.</param>
/// <param name="CurrentValue">Value on the order when the request was created.</param>
/// <param name="RequestedValue">Value the order should have after approval.</param>
/// <param name="Reason">Why the change was requested.</param>
/// <param name="Status">Current status — <c>Pending</c>, <c>Approved</c> or <c>Rejected</c>.</param>
/// <param name="CreatedBy">Who raised the request.</param>
/// <param name="CreatedAt">When the request was created.</param>
/// <param name="DecidedBy">Who decided, once decided.</param>
/// <param name="DecidedAt">When it was decided, once decided.</param>
/// <param name="DecisionComment">Note added with the decision, when present.</param>
public sealed record ChangeRequestDto(
    int Id,
    int OrderId,
    ChangeRequestType Type,
    string CurrentValue,
    string RequestedValue,
    string Reason,
    ChangeRequestStatus Status,
    string CreatedBy,
    DateTimeOffset CreatedAt,
    string? DecidedBy,
    DateTimeOffset? DecidedAt,
    string? DecisionComment);
