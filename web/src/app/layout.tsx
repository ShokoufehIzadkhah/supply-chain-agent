import type { Metadata } from "next";
import Link from "next/link";
import "./globals.css";

export const metadata: Metadata = {
  title: "Supply Chain Assistant",
  description: "Chat with the order assistant and review the changes it proposes.",
};

// The root layout wraps every page. It is a Server Component (no "use client"), so it renders on the
// server and ships only plain markup plus the two links below to the browser.
export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en">
      <body className="min-h-screen bg-slate-50 text-slate-900">
        <header className="border-b border-slate-200 bg-white">
          <nav className="mx-auto flex max-w-3xl gap-4 px-4 py-3 text-sm">
            <Link href="/" className="font-medium hover:underline">
              Chat
            </Link>
            <Link href="/approvals" className="font-medium hover:underline">
              Approvals
            </Link>
          </nav>
        </header>
        <div className="mx-auto max-w-3xl px-4 py-6">{children}</div>
      </body>
    </html>
  );
}
