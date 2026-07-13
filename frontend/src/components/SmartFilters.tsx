/**
 * SmartFilters — saved/preset filter views for the invoice list.
 * Provides one-click access to common filtered views.
 */

"use client";

import { cn } from "@/lib/utils";
import type { InvoiceStatus } from "@/lib/types";

interface SmartFilter {
  key: string;
  label: string;
  icon: string;
  status?: InvoiceStatus | InvoiceStatus[];
  description: string;
}

const SMART_FILTERS: SmartFilter[] = [
  {
    key: "all",
    label: "All Invoices",
    icon: "📋",
    description: "Every invoice in the system",
  },
  {
    key: "needs-review",
    label: "Needs Review",
    icon: "👁️",
    status: ["extracted", "reviewing"],
    description: "AI extraction complete, needs human review",
  },
  {
    key: "pending-approval",
    label: "Pending Approval",
    icon: "⏳",
    status: "approved",
    description: "Approved and ready for compliance processing",
  },
  {
    key: "failed",
    label: "Failed / Errors",
    icon: "⚠️",
    status: "failed",
    description: "Extraction or compliance failures",
  },
  {
    key: "processing",
    label: "In Progress",
    icon: "🔄",
    status: ["received", "processing"],
    description: "Currently being ingested or extracted",
  },
  {
    key: "completed",
    label: "Completed",
    icon: "✅",
    status: ["compliant", "transmitted"],
    description: "Successfully processed and transmitted",
  },
];

interface SmartFiltersProps {
  activeFilter: string;
  onFilterChange: (key: string, statuses?: InvoiceStatus[]) => void;
  counts?: Record<string, number>;
}

export { SMART_FILTERS };

export function getStatusesForFilter(key: string): InvoiceStatus[] | undefined {
  const filter = SMART_FILTERS.find((f) => f.key === key);
  if (!filter?.status) return undefined;
  return Array.isArray(filter.status) ? filter.status : [filter.status];
}

export function SmartFilters({ activeFilter, onFilterChange, counts }: SmartFiltersProps) {
  return (
    <div className="flex gap-2 overflow-x-auto pb-1">
      {SMART_FILTERS.map((filter) => {
        const isActive = activeFilter === filter.key;
        const count = counts?.[filter.key];

        return (
          <button
            key={filter.key}
            onClick={() =>
              onFilterChange(
                filter.key,
                Array.isArray(filter.status)
                  ? (filter.status as InvoiceStatus[])
                  : filter.status
                    ? [filter.status]
                    : undefined,
              )
            }
            className={cn(
              "flex items-center gap-2 px-3 py-2 rounded-lg text-sm font-medium whitespace-nowrap transition-colors border",
              isActive
                ? "bg-primary-50 border-primary-200 text-primary-700"
                : "bg-white border-gray-200 text-gray-600 hover:bg-gray-50 hover:border-gray-300",
            )}
          >
            <span>{filter.icon}</span>
            <span>{filter.label}</span>
            {count !== undefined && (
              <span
                className={cn(
                  "text-xs px-1.5 py-0.5 rounded-full font-medium",
                  isActive
                    ? "bg-primary-100 text-primary-700"
                    : "bg-gray-100 text-gray-500",
                )}
              >
                {count}
              </span>
            )}
          </button>
        );
      })}
    </div>
  );
}
