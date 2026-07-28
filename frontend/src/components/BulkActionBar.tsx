/**
 * BulkActionBar — floating toolbar that appears when invoices are selected.
 * Supports approve, reject, process, and transmit in bulk.
 */

"use client";

import { Button } from "@/components/ui";
import { Spinner } from "@/components/ui";

interface BulkActionBarProps {
  selectedCount: number;
  loading: string | null;
  onApprove: () => void;
  onReject: () => void;
  onValidate?: () => void;
  onComply?: () => void;
  onTransmit?: () => void;
  onClear: () => void;
}

export function BulkActionBar({
  selectedCount,
  loading,
  onApprove,
  onReject,
  onValidate,
  onComply,
  onTransmit,
  onClear,
}: BulkActionBarProps) {
  if (selectedCount === 0) return null;

  return (
    <div className="sticky top-0 z-30 bg-primary-50 border border-primary-200 rounded-lg px-4 py-3 shadow-sm flex items-center justify-between animate-in slide-in-from-top-2 duration-200">
      <div className="flex items-center gap-3">
        <span className="text-sm font-medium text-primary-700">
          {selectedCount} invoice{selectedCount > 1 ? "s" : ""} selected
        </span>
        <button onClick={onClear} className="text-xs text-primary-600 hover:underline">
          Clear selection
        </button>
      </div>
      <div className="flex items-center gap-2 flex-wrap">
        <Button
          variant="success"
          size="sm"
          disabled={loading !== null}
          onClick={onApprove}
        >
          {loading === "approve" ? <Spinner size="sm" /> : null}
          ✅ Approve
        </Button>
        <Button
          variant="danger"
          size="sm"
          disabled={loading !== null}
          onClick={onReject}
        >
          {loading === "reject" ? <Spinner size="sm" /> : null}
          ❌ Reject
        </Button>
        {onValidate && (
          <Button
            variant="outline"
            size="sm"
            disabled={loading !== null}
            onClick={onValidate}
          >
            {loading === "validate" ? <Spinner size="sm" /> : null}
            🔬 Validate
          </Button>
        )}
        {onComply && (
          <Button
            variant="primary"
            size="sm"
            disabled={loading !== null}
            onClick={onComply}
          >
            {loading === "comply" ? <Spinner size="sm" /> : null}
            🛡️ Process Compliance
          </Button>
        )}
        {onTransmit && (
          <Button
            variant="success"
            size="sm"
            disabled={loading !== null}
            onClick={onTransmit}
          >
            {loading === "transmit" ? <Spinner size="sm" /> : null}
            🚀 Transmit
          </Button>
        )}
      </div>
    </div>
  );
}
