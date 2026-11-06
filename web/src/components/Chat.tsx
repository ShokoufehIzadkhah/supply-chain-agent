"use client";

// "use client" marks this file as a Client Component: it runs in the browser and may use state,
// effects and event handlers. Every page in the App Router is a Server Component by default; only a
// file like this one opts into the browser, which is what interactivity needs.
import { useEffect, useRef, useState } from "react";
import { sendChat } from "@/lib/api";
import type { ToolCall } from "@/lib/types";

// One line of the message list. A user turn has only text; an assistant turn also carries the tool
// calls it made and the policy documents it used, which the UI shows as chips under the answer.
type Message = {
  id: string;
  role: "user" | "assistant";
  text: string;
  toolCalls?: ToolCall[];
  sources?: string[];
};

export default function Chat() {
  // State is data owned by this component. Calling the setter changes it and React re-renders the
  // component, which is how the screen stays in step with what the user typed or the API returned.
  const [messages, setMessages] = useState<Message[]>([]);
  const [input, setInput] = useState("");
  const [isSending, setIsSending] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // A ref holds a value that must survive re-renders without triggering one. The conversation id has
  // to stay the same for the whole visit, so it is created once and never displayed.
  const conversationIdRef = useRef<string>(crypto.randomUUID());

  // Refs can also point at a DOM node. This one marks the bottom of the list so we can scroll to it.
  const bottomRef = useRef<HTMLDivElement>(null);

  // An effect runs after React has updated the DOM, not during render. Here it scrolls to the newest
  // message whenever the list changes — something that is only possible once the new node exists.
  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: "smooth" });
  }, [messages]);

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();

    const text = input.trim();
    if (!text || isSending) {
      return;
    }

    // Show the user's turn right away, before the answer comes back.
    setMessages((previous) => [...previous, { id: crypto.randomUUID(), role: "user", text }]);
    setInput("");
    setIsSending(true);
    setError(null);

    try {
      const response = await sendChat(conversationIdRef.current, text);

      setMessages((previous) => [
        ...previous,
        {
          id: crypto.randomUUID(),
          role: "assistant",
          text: response.answer,
          toolCalls: response.toolCalls,
          sources: response.sources,
        },
      ]);
    } catch {
      setError("Could not reach the assistant. Is the API running?");
    } finally {
      setIsSending(false);
    }
  }

  return (
    <section className="flex h-[calc(100vh-11rem)] flex-col">
      <div className="flex-1 space-y-4 overflow-y-auto pr-1">
        {messages.length === 0 && (
          <p className="text-sm text-slate-500">
            Try “When does order 1001 ship?” or “What is the minimum order quantity for corrugated
            boxes?”
          </p>
        )}

        {messages.map((message) => (
          <MessageRow key={message.id} message={message} />
        ))}

        {/* An empty element at the end: the scroll target from the effect above. */}
        <div ref={bottomRef} />
      </div>

      {error && <p className="mt-2 text-sm text-red-600">{error}</p>}

      <form onSubmit={handleSubmit} className="mt-4 flex gap-2">
        <input
          // A controlled input: React state, not the DOM, is the source of truth for its value.
          value={input}
          onChange={(event) => setInput(event.target.value)}
          placeholder="Ask a question…"
          className="flex-1 rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none focus:border-slate-500"
        />
        <button
          type="submit"
          disabled={isSending || input.trim() === ""}
          className="rounded-lg bg-slate-900 px-4 py-2 text-sm font-medium text-white disabled:opacity-40"
        >
          {isSending ? "Thinking…" : "Send"}
        </button>
      </form>
    </section>
  );
}

// A small presentational component: it receives its data as props and renders it, keeping no state of
// its own. Splitting the row out keeps the Chat component above easier to read.
function MessageRow({ message }: { message: Message }) {
  const isUser = message.role === "user";

  return (
    <div className={isUser ? "flex justify-end" : "flex justify-start"}>
      <div className="max-w-[85%]">
        <div
          className={
            isUser
              ? "rounded-2xl bg-slate-900 px-4 py-2 text-sm text-white"
              : "rounded-2xl bg-white px-4 py-2 text-sm text-slate-800 ring-1 ring-slate-200"
          }
        >
          {message.text}
        </div>

        {/* Chips are only meaningful for an assistant answer. */}
        {!isUser && <Chips toolCalls={message.toolCalls} sources={message.sources} />}
      </div>
    </div>
  );
}

// The chips under an answer: one per tool the agent called, one per policy document it cited. They
// make the answer's reasoning visible instead of a black box.
function Chips({ toolCalls, sources }: { toolCalls?: ToolCall[]; sources?: string[] }) {
  const hasTools = toolCalls && toolCalls.length > 0;
  const hasSources = sources && sources.length > 0;

  if (!hasTools && !hasSources) {
    return null;
  }

  return (
    <div className="mt-2 flex flex-wrap gap-1.5">
      {toolCalls?.map((tool, index) => (
        <span
          key={`tool-${index}`}
          className="rounded-full bg-indigo-50 px-2 py-0.5 text-xs text-indigo-700"
        >
          {tool.name} · {Math.round(tool.durationMs)} ms
        </span>
      ))}

      {sources?.map((source, index) => (
        <span
          key={`source-${index}`}
          className="rounded-full bg-emerald-50 px-2 py-0.5 text-xs text-emerald-700"
        >
          {source}
        </span>
      ))}
    </div>
  );
}
