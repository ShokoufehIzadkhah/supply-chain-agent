// A Server Component that renders the approvals page. The table itself is a Client Component because
// it fetches data and reacts to clicks.
import ApprovalTable from "@/components/ApprovalTable";

export default function ApprovalsPage() {
  return (
    <main>
      <h1 className="text-lg font-semibold text-slate-900">Pending approvals</h1>
      <p className="mb-4 text-sm text-slate-500">
        Changes the assistant proposed. Approve one to apply it to the order, or reject it to leave the
        order as it is.
      </p>
      <ApprovalTable />
    </main>
  );
}
