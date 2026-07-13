/**
 * ProgressStepper — visual pipeline indicator showing where an invoice
 * is in the receive→extract→approve→comply→transmit→archive flow.
 */

"use client";

import { cn } from "@/lib/utils";
import type { InvoiceStatus } from "@/lib/types";

const steps: { key: InvoiceStatus | "archived"; label: string; icon: string }[] = [
  { key: "received", label: "Received", icon: "📥" },
  { key: "processing", label: "Processing", icon: "🔄" },
  { key: "extracted", label: "Extracted", icon: "🤖" },
  { key: "reviewing", label: "Review", icon: "👁️" },
  { key: "approved", label: "Approved", icon: "✅" },
  { key: "compliant", label: "Compliant", icon: "🛡️" },
  { key: "transmitted", label: "Transmitted", icon: "🚀" },
];

const statusOrder: Record<string, number> = {
  received: 0,
  processing: 1,
  extracted: 2,
  reviewing: 3,
  approved: 4,
  compliant: 5,
  transmitted: 6,
  archived: 7,
  failed: -1,
  rejected: -1,
};

export function ProgressStepper({ status }: { status: InvoiceStatus | "archived" }) {
  const currentIdx = statusOrder[status] ?? -1;

  // Terminal states don't show the stepper
  if (currentIdx < 0) return null;

  return (
    <div className="flex items-center w-full">
      {steps.map((step, i) => {
        const isComplete = i <= currentIdx;
        const isCurrent = i === currentIdx;

        return (
          <div key={step.key} className="flex-1 flex items-center">
            {/* Step circle + label */}
            <div className="flex flex-col items-center">
              <div
                className={cn(
                  "w-8 h-8 rounded-full flex items-center justify-center text-sm font-medium transition-all duration-300",
                  isComplete
                    ? "bg-primary text-white shadow-sm"
                    : "bg-gray-100 text-gray-400",
                  isCurrent && "ring-2 ring-primary/30 ring-offset-2",
                )}
              >
                {isComplete ? "✓" : i + 1}
              </div>
              <span
                className={cn(
                  "text-[10px] mt-1 font-medium whitespace-nowrap",
                  isComplete ? "text-primary" : "text-gray-400",
                )}
              >
                {step.label}
              </span>
            </div>

            {/* Connector line */}
            {i < steps.length - 1 && (
              <div
                className={cn(
                  "flex-1 h-0.5 mx-1 transition-colors duration-300",
                  i < currentIdx ? "bg-primary" : "bg-gray-200",
                )}
              />
            )}
          </div>
        );
      })}
    </div>
  );
}
