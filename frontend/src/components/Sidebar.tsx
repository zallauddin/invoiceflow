/**
 * Sidebar — client component with active nav highlighting and user menu.
 */

"use client";

import { usePathname } from "next/navigation";
import { UserMenu } from "@/components/UserMenu";
import { cn } from "@/lib/utils";

const NAV_ITEMS = [
  { href: "/", label: "Dashboard", icon: "📊" },
  { href: "/invoices", label: "Invoices", icon: "📄" },
  { href: "/connectors", label: "Connectors", icon: "🔌" },
  { href: "/webhooks", label: "Webhooks", icon: "🪝" },
  { href: "/settings", label: "Settings", icon: "⚙️" },
];

export function Sidebar() {
  const pathname = usePathname();

  // Active detection: exact match for dashboard, prefix match for others
  function isActive(href: string): boolean {
    if (href === "/") return pathname === "/";
    return pathname.startsWith(href);
  }

  return (
    <aside className="w-64 bg-white border-r border-gray-200 flex flex-col shrink-0">
      {/* Brand */}
      <div className="p-4 border-b border-gray-100">
        <div className="flex items-center gap-2">
          <div className="w-8 h-8 bg-primary rounded-lg flex items-center justify-center">
            <span className="text-white font-bold text-sm">IF</span>
          </div>
          <h1 className="text-xl font-bold text-primary">InvoiceFlow</h1>
        </div>
      </div>

      {/* Navigation */}
      <nav className="flex-1 p-3 space-y-1 overflow-y-auto">
        {NAV_ITEMS.map((item) => {
          const active = isActive(item.href);
          return (
            <a
              key={item.href}
              href={item.href}
              className={cn(
                "flex items-center gap-3 px-3 py-2.5 rounded-lg text-sm font-medium transition-colors",
                active
                  ? "bg-primary-50 text-primary-700"
                  : "text-gray-600 hover:bg-gray-50 hover:text-gray-900",
              )}
            >
              <span className="text-base">{item.icon}</span>
              {item.label}
            </a>
          );
        })}
      </nav>

      {/* User menu + version */}
      <div className="border-t border-gray-100 p-3 space-y-2">
        <UserMenu />
        <p className="text-xs text-gray-400 text-center">InvoiceFlow v0.1.0</p>
      </div>
    </aside>
  );
}
