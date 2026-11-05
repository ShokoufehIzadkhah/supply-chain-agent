# Supply Chain Order Assistant

An AI agent that helps a manufacturing company's Customer Service team answer questions about orders, inventory, and shipments, and propose order changes that a human must approve. The agent talks to a mock ERP over HTTP only, never applies changes itself, and is structured as production-minded .NET (API, shared tools, console host, tests) with a chat UI to follow.

## Getting started

Placeholder — local run steps will be added as the projects are wired up.

## Security design

The agent can ask for a change, but it can never make one. That separation is enforced in three layers, none of which relies on the model behaving well.

- **Two roles, two keys.** The ERP API authenticates every request with an `X-Api-Key` header and a single authentication scheme. The **Agent** key may create and read change requests; the **Supervisor** key may read them and decide on them. A key that is missing or unknown gets `401`; a valid key used on an endpoint its role does not cover gets `403`.
- **Decisions are not in the tools library.** `Agent.Tools` only exposes `ProposeDeliveryDateChange`, which submits a `Pending` request, plus the read-only lookup tools. It holds the Agent key alone and contains no method that approves, rejects or applies a change, so there is no code path the agent could reach to approve its own request.
- **Hard rules live in the API.** `POST /change-requests` returns `400` for changes the ERP never accepts — an order that is already `Delivered` or `Cancelled`, or a new date earlier than the order date — regardless of what the agent asks. Softer business policies stay with the agent (checked before it proposes) and with the supervisor who decides.

Keys are never committed: supply them with `dotnet user-secrets` (`ApiKeys:Agent` for the API and the console host, `ApiKeys:Supervisor` for the API) or environment variables. `AddAgentTools` fails at startup when `ApiKeys:Agent` is missing.
