import type { ChangeRequest, ChatResponse } from "./types";

// The backend's address, read from the environment. Variables prefixed NEXT_PUBLIC_ are inlined into
// the browser bundle at build time, so this constant is available in both Server and Client Components.
// The fallback keeps the app runnable without an .env.local file during development.
const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5000";

// Sends one chat turn. The same conversationId on every call keeps this tab's history together on the
// server, which is why follow-up questions can refer to earlier ones.
export async function sendChat(conversationId: string, message: string): Promise<ChatResponse> {
  const response = await fetch(`${API_BASE_URL}/chat`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ conversationId, message }),
  });

  if (!response.ok) {
    throw new Error(`Chat request failed with status ${response.status}.`);
  }

  return (await response.json()) as ChatResponse;
}

// Lists the change requests that still wait for a supervisor's decision.
export async function listPendingChangeRequests(): Promise<ChangeRequest[]> {
  const response = await fetch(`${API_BASE_URL}/change-requests?status=Pending`);

  if (!response.ok) {
    throw new Error(`Listing change requests failed with status ${response.status}.`);
  }

  return (await response.json()) as ChangeRequest[];
}

// Approves or rejects one change request, with an optional comment. decidedBy is left empty on
// purpose: the backend fills in the supervisor identity.
export async function decideChangeRequest(
  id: number,
  action: "approve" | "reject",
  decisionComment: string,
): Promise<void> {
  const response = await fetch(`${API_BASE_URL}/change-requests/${id}/${action}`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ decidedBy: null, decisionComment: decisionComment || null }),
  });

  if (!response.ok) {
    throw new Error(`Deciding change request ${id} failed with status ${response.status}.`);
  }
}
