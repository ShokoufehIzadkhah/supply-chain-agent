# Manual test questions for the Order Assistant

Ten questions to run against `Agent.Console` by hand, plus what a **correct** answer must contain.
The goal is to expose weak tool descriptions and weak system-prompt behaviour, so watch the grey
tool-call lines (name, arguments, duration) as closely as the final answer text.

## How to run

1. Start the ERP API: `dotnet run --project src/Erp.Api` (note the URL it prints, e.g. `http://localhost:5187`).
2. Point the agent at it: `ErpApi:BaseUrl` in `src/Agent.Console/appsettings.json` (or user-secrets).
3. Make sure `AI:*` settings (provider, endpoint, key, chat model) are set via user-secrets.
4. `dotnet run --project src/Agent.Console` and paste each question below one at a time.

## Ground truth (from the seeded dev database)

Orders are numbered from **1001**. Absolute dates are fixed when the database is first seeded
(the seeder uses `today + N days`), so the dates below are a snapshot — verify against
`GET /orders/1023` if you reseed.

| Fact | Value |
| --- | --- |
| Demo order | **1023** — `InProduction`, customer `Fjordkyst Sjømat AS` (Norway, priority), orderDate `2026-10-24`, requestedDeliveryDate `2026-12-03`, productionStartDate `2026-11-13`, 3 lines |
| Customers (10) | Nordvik Elintarvikkeet Oy (FI, priority), Bergstrand Livsmedel AB (SE), Fjordkyst Sjømat AS (NO, priority), København Kolonial A/S (DK), Pohjan Verkkokauppa Oy (FI), Alpenhof Lebensmittel GmbH (DE, priority), Van Dijk Versgroep B.V. (NL), Maison Duval Distribution SAS (FR), Balti E-Commerce OÜ (EE), Iberia Retail Ibérica S.L. (ES) |
| Nordvik Elintarvikkeet Oy orders | 1006 `Shipped`, 1011 `Delayed`, 1017 `Delivered`, 1040 `Delayed`, 1042 `Confirmed`, 1044 `InProduction` |
| All 6 `Delayed` orders | 1009 (Iberia Retail), 1011 (Nordvik), 1027 (Pohjan Verkkokauppa), 1029 (Van Dijk), 1034 (Iberia Retail), 1040 (Nordvik) |
| Products on Nordvik's delayed orders | 1011 → `FC-220x120x90-400`, `FC-CEREAL-240-350`; 1040 → `FC-180x70x60-350` |
| Stock, `FC-220x120x90-400` | total available **54 932** (Gdańsk 337, Imatra 25 176, Oulu 29 419) |
| Stock, `FC-CEREAL-240-350` | total available **15 207** (Gdańsk 14 779, Imatra **0**, Oulu 428) |
| Stock, `FC-180x70x60-350` | total available **10 946** (Gdańsk 3 806, Imatra 6 820, Oulu 320) |
| Order `9999` | does **not** exist → API returns 404 |
| Shipment for 1006 | `InTransit`, carrier `Hansa Nordic Cargo`, tracking `HNC-100004`, ETA `2026-11-14` |
| Shipment for 1023 | **none** (order not shipped yet) |
| `bubble wrap` | not a product in the catalogue |

---

## 1. Normal single-order lookup (happy path)

**Question:** `What is the status of order 1023?`

**Expected tool:** `GetOrderStatus(orderId: 1023)` — exactly once.

**A correct answer must contain:**
- Status `InProduction`.
- Customer `Fjordkyst Sjømat AS` and that it is a priority account.
- The order id 1023, restated so the customer knows which order is meant.
- Optionally the dates: ordered 2026-10-24, requested delivery 2026-12-03, production starts 2026-11-13.

**Failure signals:** invents a delivery date or a customer; calls a different tool; does not state the status.

## 2. Order that does not exist

**Question:** `What is the status of order 9999?`

**Expected tool:** `GetOrderStatus(orderId: 9999)` — exactly once.

**A correct answer must contain:**
- A clear statement that order 9999 was **not found** / does not exist in the ERP.
- A short, polite next step (e.g. "please double-check the order number").

**Failure signals:** invents a status, customer or date for 9999; reports a technical/HTTP error to the
user; crashes or apologises without saying the order is missing.

## 3. Normal customer lookup (all orders)

**Question:** `Which orders does Nordvik Elintarvikkeet have?`

**Expected tool:** `FindOrdersByCustomer(customerName: "Nordvik Elintarvikkeet")` (a distinctive part is fine) — no status filter.

**A correct answer must contain:**
- All six orders: **1006, 1011, 1017, 1040, 1042, 1044**, each with its status.
- The status spread: 1006 `Shipped`, 1011 `Delayed`, 1017 `Delivered`, 1040 `Delayed`, 1042 `Confirmed`, 1044 `InProduction`.

**Failure signals:** drops or invents orders; mixes in another customer; answers with a count only when
the user asked "which".

## 4. Filtered customer lookup

**Question:** `Show me the delayed orders of Nordvik Elintarvikkeet Oy.`

**Expected tool:** `FindOrdersByCustomer(customerName: "Nordvik Elintarvikkeet Oy", status: "Delayed")`.

**A correct answer must contain:**
- Exactly **1011** and **1040** (and no other Nordvik order).
- It must **not** list the other delayed orders (1009, 1027, 1029, 1034) belonging to other customers.

**Failure signals:** ignores the status filter; returns all six Nordvik orders; returns all six delayed
orders regardless of customer.

## 5. Inventory lookup by product

**Question:** `How much stock do we have of the 220x120x90 folding carton?`

**Expected tool:** `CheckInventory(productName: "FC-220x120x90-400")` (name or the distinctive code both acceptable).

**A correct answer must contain:**
- Total available **54 932** for `FC-220x120x90-400`.
- Preferably the per-warehouse split: Imatra 25 176, Oulu 29 419, Gdańsk 337.
- The product identified clearly enough that the customer knows which item it is.

**Failure signals:** invents a number; returns a different product; confuses available (on-hand minus
reserved) with on-hand.

## 6. Two-tool question (the important one)

**Question:** `Which of Nordvik Elintarvikkeet's orders are delayed, and do we have stock for the products on them?`

**Expected tools:** **both** `FindOrdersByCustomer(customerName: "Nordvik…", status: "Delayed")`
**and** `CheckInventory(...)` for each product on those orders — i.e. two or more tool calls in one turn.

**A correct answer must contain:**
- The delayed orders **1011** and **1040** (Nordvik only).
- The products involved and their stock:
  - `FC-220x120x90-400` → **54 932** available.
  - `FC-CEREAL-240-350` → **15 207** available (note Imatra is at **0**).
  - `FC-180x70x60-350` → **10 946** available.
- A statement that ties the two together, e.g. stock is available for these products.

**Failure signals:** only calls one tool and guesses the rest; reports stock for an unrelated product;
forgets to mention the low/zero location; truncates the second half of the question.

## 7. Ambiguous customer name

**Question:** `Show me the orders for Oy.`

**What "Oy" actually matches:** two customers — **Nordvik Elintarvikkeet Oy** (1006, 1011, 1017, 1040,
1042, 1044) and **Pohjan Verkkokauppa Oy** (1027, 1032).

**A correct answer must contain:**
- Recognition that "Oy" is **ambiguous** and matches more than one customer.
- Either asks the user which of the two is meant, **or** lists both customers' orders clearly separated
  and flagged as different companies.

**Failure signals:** silently picks one customer and presents it as the answer; invents a single
customer named "Oy"; returns an unrelated or empty result without explaining why.

## 8. Shipment lookup

**Question:** `What is the shipment status for order 1006?`

**Expected tool:** `GetShipmentInfo(orderId: 1006)`.

**A correct answer must contain:**
- Status `InTransit`, carrier `Hansa Nordic Cargo`, tracking number `HNC-100004`, ETA `2026-11-14`.
- The order id 1006 restated.

**Failure signals:** invents a carrier or tracking number; says the order does not exist; returns
`Delivered` with no delivered date.

## 9. Order exists but has no shipment

**Question:** `Has order 1023 shipped yet?`

**Expected tools:** `GetOrderStatus(orderId: 1023)` and/or `GetShipmentInfo(orderId: 1023)`.

**A correct answer must contain:**
- A clear "no, it has not shipped" — order 1023 is still `InProduction`, and there is **no shipment
  record** for it.
- If useful, the production start date 2026-11-13 as the next milestone.

**Failure signals:** says the order "does not exist" (it does — there is just no shipment); invents a
tracking number; confuses "no shipment yet" with "not found".

## 10. Off-topic questions and out-of-scope requests

**Question (a):** `What's the weather in Helsinki tomorrow?`

**Question (b):** `Cancel order 1023 for me.`

**A correct answer must contain:**
- For (a): a short, polite statement that this assistant only handles orders, inventory and shipments —
  **no tool call** is made for weather.
- For (b): a clear statement that the assistant **cannot change, approve or cancel** orders, and a
  suggestion to contact the responsible person. No claim that anything was cancelled.

**Failure signals:** answers the weather from general knowledge; calls a tool for an off-topic
question; pretends to cancel or change an order; invents a policy or a phone number.

---

## What to look for while running

- **Wrong tool / right tool twice:** does the model pick the right tool first time, and call it once?
- **Argument quality:** are customer names passed with enough detail (not just "Oy" silently resolved)?
- **Number fidelity:** every number in the answer must appear in a tool result — anything else is a bug
  in the tool description or the system prompt.
- **Tone on failure:** missing data should read as "not found / not available", never as a raw error.
