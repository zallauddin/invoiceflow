/**
 * ConfidenceField — renders a data field with AI confidence coloring.
 * Green = high confidence (≥0.85), Yellow = medium (≥0.6), Red = low (<0.6).
 */

"use client";

import { type ReactNode } from "react";
import { cn } from "@/lib/utils";

interface ConfidenceFieldProps {
  label: string;
  value: ReactNode;
  confidence?: number | null;
}

function confidenceColor(confidence?: number | null): string {
  if (confidence == null) return "";
  if (confidence >= 0.85) return "bg-green-50 border-green-200";
  if (confidence >= 0.6) return "bg-yellow-50 border-yellow-200";
  return "bg-red-50 border-red-200";
}

function confidenceLabel(confidence?: number | null): string | null {
  if (confidence == null) return null;
  if (confidence >= 0.85) return "High";
  if (confidence >= 0.6) return "Med";
  return "Low";
}

function confidenceLabelColor(confidence?: number | null): string {
  if (confidence == null) return "";
  if (confidence >= 0.85) return "text-green-700 bg-green-100";
  if (confidence >= 0.6) return "text-yellow-700 bg-yellow-100";
  return "text-red-700 bg-red-100";
}

export function ConfidenceField({ label, value, confidence }: ConfidenceFieldProps) {
  const labelText = confidenceLabel(confidence);

  return (
    <div
      className={cn(
        "rounded-lg p-3 border transition-colors",
        confidenceColor(confidence),
        !confidence && "bg-gray-50 border-transparent",
      )}
    >
      <div className="flex items-center justify-between mb-0.5">
        <p className="text-xs text-gray-500 uppercase tracking-wide">{label}</p>
        {labelText && (
          <span
            className={cn(
              "text-[10px] font-medium px-1.5 py-0.5 rounded-full",
              confidenceLabelColor(confidence),
            )}
          >
            {labelText} {(confidence! * 100).toFixed(0)}%
          </span>
        )}
      </div>
      <p className="text-sm font-medium text-gray-900">{value || "—"}</p>
    </div>
  );
}

/**
 * Inline confidence badge for use inside tables or lists.
 */
export function InlineConfidence({ confidence }: { confidence?: number | null }) {
  if (confidence == null) return null;
  const pct = (confidence * 100).toFixed(0);
  if (confidence >= 0.85) {
    return (
      <span className="inline-flex items-center gap-1 text-xs text-green-700 bg-green-100 px-1.5 py-0.5 rounded-full">
        <span className="w-1.5 h-1.5 rounded-full bg-green-500" />
        {pct}%
      </span>
    );
  }
  if (confidence >= 0.6) {
    return (
      <span className="inline-flex items-center gap-1 text-xs text-yellow-700 bg-yellow-100 px-1.5 py-0.5 rounded-full">
        <span className="w-1.5 h-1.5 rounded-full bg-yellow-500" />
        {pct}%
      </span>
    );
  }
  return (
    <span className="inline-flex items-center gap-1 text-xs text-red-700 bg-red-100 px-1.5 py-0.5 rounded-full">
      <span className="w-1.5 h-1.5 rounded-full bg-red-500" />
      {pct}%
    </span>
  );
}
