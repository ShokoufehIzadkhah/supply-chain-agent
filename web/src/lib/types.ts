// TypeScript types that mirror the JSON the Chat.Api backend returns. Keeping them in one file means
// a component and the API call that feeds it never disagree about the shape of the data.

// One tool the agent called while answering, as reported in the `toolCalls` array.
export type ToolCall = {
  name: string;
  args: unknown;
  durationMs: number;
};

// Body of POST /chat.
export type ChatResponse = {
  answer: string;
  toolCalls: ToolCall[];
  sources: string[];
};

// One row of GET /change-requests, matching the ERP API's ChangeRequestDto.
export type ChangeRequest = {
  id: number;
  orderId: number;
  type: string;
  currentValue: string;
  requestedValue: string;
  reason: string;
  status: string;
  createdBy: string;
  createdAt: string;
  decidedBy: string | null;
  decidedAt: string | null;
  decisionComment: string | null;
};
