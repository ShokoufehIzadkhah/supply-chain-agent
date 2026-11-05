# Priority Customers

| | |
|---|---|
| **Document** | PRI-01 — Priority Customer Policy |
| **Version** | 2.4 |
| **Effective** | 1 January 2026 |
| **Owner** | Sales Director, Finland |
| **Applies to** | All accounts flagged as priority in the ERP |

## 1. Purpose

1.1 Priority accounts are the company's largest and most strategically important customers. They receive shorter lead times, guaranteed stock allocation and a wider change window than standard accounts.

1.2 Priority status is stored as a flag on the customer record (`IsPriority`). The assistant must check this flag before applying any rule in section 3, 4 or 6, because every benefit below is an exception to the standard rules.

## 2. Criteria, current accounts and review

2.1 An account qualifies for priority status when it meets **at least one** of:

- annual net sales of **€500,000 or more** in the previous calendar year, or
- a signed frame agreement covering **at least 12 months**, or
- strategic value confirmed by the Sales Director (for example a new market entry).

2.2 Accounts currently holding priority status:

| Customer | Country |
|---|---|
| Nordvik Elintarvikkeet Oy | Finland |
| Fjordkyst Sjømat AS | Norway |
| Alpenhof Lebensmittel GmbH | Germany |

2.3 Status is reviewed every **January and July**. An account that falls below €400,000 in annual net sales for two consecutive reviews loses priority status from the **end of the following quarter**.

2.4 Priority status is granted and removed by the Sales Director only. The assistant never promises, grants or revokes it.

## 3. Service commitments

3.1 Production lead time is reduced by **2 working days** against the standard table in DEL-01, section 2.1.

3.2 Stock is allocated on order confirmation: the ordered quantity is reserved in full even if it exceeds free stock at the time of confirmation.

3.3 Freight is free of charge regardless of order value (standard threshold €2,500, see DEL-01, section 5.2).

3.4 Each priority account has a named customer service contact, and the assistant identifies the account as priority in its answers.

## 4. Wider delivery date change window

4.1 This is the exception to CHG-01, section 3. For priority accounts a delivery date change is allowed while:

```
allowed  ⟺  today + 3 ≤ ProductionStartDate
```

4.2 The standard window is 5 calendar days; priority accounts get 3. Every other rule in CHG-01 still applies unchanged: the hard limits in section 2, the refusal for shipped, delivered or cancelled orders in section 4, the 14-day postponement limit in section 7 and the one-pending-request rule in section 8.

## 5. Earlier delivery with incomplete stock

5.1 For standard accounts an earlier delivery is refused unless available stock covers the order completely (CHG-01, section 6.2).

5.2 For priority accounts the request is passed to the supervisor even when stock does not fully cover the order. Usage:

- Cover of **80% or more** of every line: the request may be created by the assistant and goes to the supervisor as a normal request.
- Cover of **less than 80%** on any line: the assistant explains the shortfall and does not create the request; the account manager handles it outside the request process.

5.3 A fully covered priority request (100% on every line) is created like any other request and needs no special handling beyond 4.1.

## 6. Delay handling and compensation

6.1 A priority order that becomes `Delayed` is escalated to the account manager automatically.

6.2 The customer is informed within **4 hours** of the status change (standard accounts: 1 working day).

6.3 A delay of more than **2 calendar days** beyond the confirmed delivery date gives the priority account a **5% credit** on the delayed lines. For standard accounts the threshold is 5 calendar days and the credit is 2% (DEL-01, section 7.3).

6.4 Credits are issued by a supervisor. The assistant never promises a credit amount as a decision — it may only state what the policy allows.

## 7. What the assistant must do

7.1 Check the priority flag before applying any deadline. When an account is priority, say so and apply the priority values (3 days, 4 hours, 5% credit, free freight) instead of the standard ones.

7.2 State which policy section the answer relies on, for example "PRI-01 section 4.1" or "CHG-01 section 3.1".

7.3 Never apply priority exceptions to a customer whose ERP record is not flagged as priority, even if the name sounds strategic.
