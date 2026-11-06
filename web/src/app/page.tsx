// A Server Component: no "use client", so this file renders on the server. It stays thin and hands the
// interactive parts to the Client Component below, which is the usual split in the App Router.
import Chat from "@/components/Chat";

export default function ChatPage() {
  return (
    <main>
      <h1 className="text-lg font-semibold text-slate-900">Chat</h1>
      <p className="mb-4 text-sm text-slate-500">
        Ask about orders, inventory, shipments and the policies that apply.
      </p>
      <Chat />
    </main>
  );
}
