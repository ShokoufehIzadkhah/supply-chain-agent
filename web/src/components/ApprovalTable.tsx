"use client";

// Client Component: it holds the table rows in state, loads them once on mount, and calls the API
// when a supervisor clicks Approve or Reject.
import { useEffect, useState } from "react";
import { decideChangeRequest, listPendingChangeRequests } from "@/lib/api";
import type { ChangeRequest } from "@/lib/types";

export default function ApprovalTable() {
  const [requests, setRequests] = useState<ChangeRequest[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // The optional comment is typed per row, so the comments are part of state too, keyed by request id.
  const [comments, setComments] = useState<Record<number, string>>({});

  // Which row is being decided right now, so its two buttons can be disabled while the call is in flight.
  const [busyId, setBusyId] = useState<number | null>(null);

  // Load the pending requests once, after the first render. The empty dependency array means the
  // effect never re-runs. The returned function is cleanup: React calls it if the component unmounts,
  // so a response that arrives late cannot set state on a component that is already gone.
  useEffect(() => {
    let cancelled = false;

    (async () => {
      try {
        const data = await listPendingChangeRequests();
        if (!cancelled) {
          setRequests(data);
        }
      } catch {
        if (!cancelled) {
          setError("Could not load change requests. Is the API running?");
        }
      } finally {
        if (!cancelled) {
          setLoading(false);
        }
      }
    })();

    return () => {
      cancelled = true;
    };
  }, []);

  async function decide(id: number, action: "approve" | "reject") {
    setBusyId(id);
    setError(null);

    try {
      await decideChangeRequest(id, action, comments[id] ?? "");

      // The table only shows Pending rows, so a decided one leaves the list.
      setRequests((previous) => previous.filter((request) => request.id !== id));
    } catch {
      setError("The change request could not be decided. It may already have been handled.");
    } finally {
      setBusyId(null);
    }
  }

  if (loading) {
    return <p className="text-sm text-slate-500">Loading…</p>;
  }

  if (requests.length === 0) {
    return <p className="text-sm text-slate-500">No pending change requests.</p>;
  }

  return (
    <div className="space-y-4">
      {error && <p className="text-sm text-red-600">{error}</p>}

      <table className="w-full border-collapse text-sm">
        <thead>
          <tr className="border-b border-slate-200 text-left text-xs uppercase text-slate-500">
            <th className="py-2 pr-3">Order</th>
            <th className="py-2 pr-3">Delivery date</th>
            <th className="py-2 pr-3">Reason</th>
            <th className="py-2 pr-3">Created</th>
            <th className="py-2 pr-3">Comment</th>
            <th className="py-2">Decision</th>
          </tr>
        </thead>
        <tbody>
          {requests.map((request) => (
            <tr key={request.id} className="border-b border-slate-100 align-top">
              <td className="py-3 pr-3 font-medium text-slate-800">#{request.orderId}</td>

              {/* Current value struck through, the requested value next to it. */}
              <td className="py-3 pr-3 text-slate-700">
                <span className="text-slate-400 line-through">{request.currentValue}</span>
                {" → "}
                <span className="font-medium text-slate-900">{request.requestedValue}</span>
              </td>

              <td className="py-3 pr-3 text-slate-700">{request.reason}</td>
              <td className="py-3 pr-3 text-slate-500">{formatDate(request.createdAt)}</td>

              <td className="py-3 pr-3">
                <input
                  value={comments[request.id] ?? ""}
                  onChange={(event) =>
                    setComments((previous) => ({ ...previous, [request.id]: event.target.value }))
                  }
                  placeholder="Optional"
                  className="w-36 rounded border border-slate-300 px-2 py-1 text-sm outline-none focus:border-slate-500"
                />
              </td>

              <td className="py-3">
                <div className="flex gap-2">
                  <button
                    onClick={() => decide(request.id, "approve")}
                    disabled={busyId === request.id}
                    className="rounded bg-emerald-600 px-3 py-1 text-xs font-medium text-white disabled:opacity-40"
                  >
                    Approve
                  </button>
                  <button
                    onClick={() => decide(request.id, "reject")}
                    disabled={busyId === request.id}
                    className="rounded bg-rose-600 px-3 py-1 text-xs font-medium text-white disabled:opacity-40"
                  >
                    Reject
                  </button>
                </div>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

// The API sends an ISO timestamp; show it in the browser's own local time.
function formatDate(value: string): string {
  return new Date(value).toLocaleString();
}
