# Delivery Terms

| | |
|---|---|
| **Document** | DEL-01 — Delivery Terms |
| **Version** | 3.2 |
| **Effective** | 1 January 2026 |
| **Owner** | Customer Service, Finland |
| **Applies to** | All sales orders placed with the company (Nordic packaging manufacturer) |

## 1. Scope and definitions

1.1 This document defines the promised delivery terms for every order, and is the reference the customer service assistant must use when answering delivery questions.

1.2 Definitions used throughout:

- **Order date** — the date the order was registered in the ERP (`OrderDate`).
- **Requested delivery date** — the date the customer wants the goods (`RequestedDeliveryDate`).
- **Production start date** — the day production is scheduled to begin (`ProductionStartDate`). It is empty while an order is still in status `Confirmed`.
- **Calendar day** — any day, including weekends and public holidays.
- **Working day** — Monday to Friday, excluding Finnish public holidays.
- **Today** — the current date in the Europe/Helsinki time zone (EET/EEST).

1.3 Date arithmetic: "at least N days before X" always means `today + N ≤ X`, so the deadline day itself is still inside the allowed window. "At most N days after X" means `date ≤ X + N`.

## 2. Standard lead times

2.1 Production lead time from order confirmation to production start:

| Product category | Production lead time |
|---|---|
| Corrugated Box | 10 working days |
| Folding Carton | 10 working days |
| Paper Sack | 10 working days |
| Liquid Packaging Board (reels) | 15 working days |

2.2 Transport lead time, counted from the production start date:

| Destination | Transport lead time | Minimum total lead time (order → delivery) |
|---|---|---|
| Finland | 5 working days | 15 working days |
| Sweden, Norway, Denmark | 7 working days | 17 working days |
| Estonia, Latvia, Lithuania | 6 working days | 16 working days |
| Germany, Netherlands | 12 working days | 22 working days |
| France, Spain | 14 working days | 24 working days |

2.3 The confirmed delivery date is `ProductionStartDate + transport lead time`. If a customer requests a date earlier than the minimum total lead time in 2.2, the order can only be accepted if stock covers it (see section 6).

2.4 The minimum total lead times in 2.2 assume the 10-working-day production lead time of 2.1. For Liquid Packaging Board (15 working days) add **5 working days** to the value in 2.2 for the same destination — for example 20 working days to Finland.

## 3. Order cut-off and confirmation

3.1 Orders received on a working day before **14:00 EET** are confirmed the same day. Orders received after 14:00 EET, or on a weekend or public holiday, are confirmed on the next working day.

3.2 Order confirmation is sent within **1 working day** of acceptance and states the confirmed delivery date, the delivery address and the freight charges.

## 4. Delivery windows and partial deliveries

4.1 Deliveries arrive on the confirmed delivery date with a window of **±1 working day**.

4.2 The exact unloading slot is confirmed to the customer **2 working days** before delivery.

4.3 Partial deliveries are only made with the customer's written consent, and only when the lines are split between warehouses. Each partial delivery must contain at least **60%** of the ordered quantity, and the remaining part must follow within **10 working days**.

## 5. Freight and delivery terms

5.1 All deliveries are **DAP** (Delivered At Place) to the customer's unloading address. EXW is available only on written request.

5.2 Freight is free of charge for consignments with a net order value of **€2,500 or more**.

5.3 Below that threshold a freight charge applies per consignment: **€180** in Finland, Sweden, Norway, Denmark, Estonia, Latvia and Lithuania; **€320** in Germany, Netherlands, France and Spain. Priority accounts are exempt (see PRI-01, section 3).

## 6. Changes to the delivery date

6.1 The requested delivery date can be changed only through the change request process described in **CHG-01 — Order Change Rules**. That document defines the allowed window, the stock requirement for an earlier delivery and the approval step.

6.2 The company never changes a confirmed delivery date on the strength of a customer conversation alone: a supervisor approves every change.

## 7. Delays and notification

7.1 When an order becomes late it is set to status `Delayed`.

7.2 Customer service informs the customer within **1 working day** of the status change and provides a new estimated delivery date within **2 working days**.

7.3 An order delayed by more than **5 calendar days** beyond the confirmed delivery date entitles the customer to a **2% credit** on the delayed lines. The credit is issued by the supervisor; the assistant cannot promise it.

7.4 Priority accounts are notified within **4 hours** (see PRI-01, section 6).

## 8. Force majeure

8.1 Force majeure (strike, energy curtailment, transport blockade) suspends the promised lead times. Customer service informs affected customers within **2 working days** and gives a revised date within **30 calendar days**.

## 9. What the assistant must do

9.1 Answer delivery questions only with data returned by the tools, quoting the order number and the relevant lead time from 2.2.

9.2 If a requested delivery date would break the minimum total lead time in 2.2 **and** stock does not cover the order, explain the rule and do not propose the change.

9.3 Never state that a delivery date has changed. Only a supervisor decision changes a date; until then the request is `Pending`.
