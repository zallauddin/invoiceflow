/**
 * Root layout — server component with metadata export.
 * Auth guard + toast provider + sidebar navigation with active state.
 */
import type { Metadata } from "next";
import { AuthGuard } from "@/components/AuthGuard";
import { Sidebar } from "@/components/Sidebar";
import { ToastProvider } from "@/components/ui/Toast";
import "./globals.css";

export const metadata: Metadata = {
  title: "InvoiceFlow — E-Invoice Processing",
  description: "AI-powered e-invoice processing with global compliance",
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="en">
      <body>
        <ToastProvider>
          <AuthGuard loginPath="/login">
            <div className="flex min-h-screen">
              <Sidebar />
              <main className="flex-1 p-8 overflow-auto bg-gray-50/50">{children}</main>
            </div>
          </AuthGuard>
        </ToastProvider>
      </body>
    </html>
  );
}
