# Order Change Rules

| | |
|---|---|
| **Document** | CHG-01 — Order Change Rules |
| **Version** | 4.0 |
| **Effective** | 1 January 2026 |
| **Owner** | Customer Service, Finland |
| **Applies to** | All sales orders placed with the company |

## 1. Scope and principles

1.1 Only two parties can touch an order: the customer service assistant may **propose** a change, and a human **supervisor decides** on it. The assistant never approves, rejects or applies a change, and never changes an order directly.

1.2 Every proposal is stored as a change request with status `Pending`. A `Pending` request does not change the order in any way. The order changes only when a supervisor approves the request.

1.3 A change request must always carry a reason. A request without a reason is invalid.

1.4 Only the **requested delivery date** of an order can be changed today. Quantity changes, line changes, address changes and cancellations are outside the scope of the change request process and the assistant must not offer them.

## 2. Hard limits enforced by the ERP

2.1 These limits are checked by the ERP itself and are never overridden by this document or by a supervisor:

| # | Condition | Result |
|---|---|---|
| 2.1.1 | Order status is `Delivered` | Change refused (`400`). A delivery that has already happened cannot be re-dated. |
| 2.1.2 | Order status is `Cancelled` | Change refused (`400`). |
| 2.1.3 | New date is earlier than the order date | Change refused (`400`). |
| 2.1.4 | Order id does not exist | Change refused (`404`). |

2.2 The assistant must recognise these refusals and explain them. It must not retry the same request in a different form.

## 3. The five-day rule

3.1 A delivery date change is allowed while production has not started and the production start date is **at least 5 calendar days away**:

```
allowed  ⟺  today + 5 ≤ ProductionStartDate
```

3.2 The fifth day is inside the window: if production starts exactly 5 calendar days from today, the change is still allowed.

3.3 Inside the five-day window (fewer than 5 calendar days before the production start date) the change is **not permitted**. Production is already committed, so the assistant must explain the rule and must **not** create a change request.

3.4 **Priority accounts** follow a shorter window of **3 calendar days** instead of 5, i.e. `today + 3 ≤ ProductionStartDate` (see PRI-01, section 4).

3.5 Status does not replace the date rule. An order in status `InProduction` whose `ProductionStartDate` is still 6 or more days away is handled by 3.1; an order in status `Shipped` is refused under section 4 regardless of dates.

## 4. Orders that can no longer change

4.1 Regardless of the production start date, a change is refused for orders in status `Shipped`, `Delivered` or `Cancelled`, and for orders whose `ProductionStartDate` is in the past.

4.2 If the customer insists, the assistant explains the rule and offers a new order or a returns discussion instead (see RET-01).

## 5. Orders without a production start date

5.1 While an order is in status `Confirmed` the production start date may still be empty. Changes are then allowed only if the new date respects a minimum lead time of **14 calendar days** from today:

```
allowed  ⟺  newDate ≥ today + 14 calendar days
```

5.2 14 calendar days is a floor, not a promise: when the destination's minimum total lead time in DEL-01, section 2.2 requires more, the longer of the two applies. A new date shorter than 14 calendar days requires stock cover (section 6) and is otherwise refused.

## 6. Earlier delivery: stock must cover the order

6.1 Bringing a delivery **forward** is only allowed when available stock covers the order completely.

6.2 "Covers the order" means, for every order line: `available = on hand − reserved` at a **single warehouse** is greater than or equal to the ordered quantity. Mixing two warehouses is not enough, because a delivery is loaded at one site.

6.3 If any line is not covered, the change is refused. The assistant explains which line lacks stock and how much is missing, and does not create a change request.

6.4 Postponing a delivery (moving it later) never needs a stock check.

6.5 Priority accounts are handled differently: their earlier-delivery requests are passed to the supervisor even when stock does not fully cover the order (see PRI-01, section 5).

## 7. Limits on postponement

7.1 A delivery date may be postponed by at most **14 calendar days** beyond the current requested delivery date.

7.2 A postponement of more than 14 calendar days is not a change; it is a cancellation and a new order. The assistant must refuse the request, explain rule 7.1 and refer the customer to their account manager.

7.3 Repeated postponements of the same order are limited to **two approved changes** in total. A third request must be escalated to the supervisor and is not created by the assistant.

## 8. One open request per order

8.1 An order may have at most **one** request in status `Pending` at a time. If a `Pending` request already exists, a second proposal for the same order must be refused until a supervisor decides the first one.

8.2 A new request may be created for an order that has earlier `Approved` or `Rejected` requests, subject to 7.3.

## 9. Approval and timing

9.1 Only a supervisor can approve or reject a request. The assistant has no access to that decision.

9.2 Supervisors decide within **1 working day** of a request being created.

9.3 Until the decision exists, customer service communicates the request as *"pending supervisor approval"* and never as an agreed date.

## 10. What the assistant must do

10.1 Before proposing anything, check the order **and** this policy (and PRI-01 if the customer is a priority account): status, `OrderDate`, `RequestedDeliveryDate`, `ProductionStartDate`, and stock when the date moves earlier.

10.2 If the requested change breaks any rule in sections 2 to 8, explain which numbered rule it breaks and do not create the request.

10.3 If no rule is broken, create the request with a clear reason and report the request id, stating that it is pending supervisor approval.

10.4 Never promise a date, never claim a date has changed, and never approve, reject or apply anything.
