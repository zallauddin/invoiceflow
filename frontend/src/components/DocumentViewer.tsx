/**
 * DocumentViewer — renders the source invoice document (PDF/image) in an iframe
 * alongside extracted data for side-by-side verification.
 */

"use client";

import { useState } from "react";
import { cn } from "@/lib/utils";

interface DocumentViewerProps {
  fileUrl?: string | null;
  originalFilename?: string | null;
  className?: string;
}

export function DocumentViewer({ fileUrl, originalFilename, className }: DocumentViewerProps) {
  const [error, setError] = useState(false);

  if (!fileUrl) {
    return (
      <div className={cn("flex items-center justify-center bg-gray-50 rounded-lg border border-gray-200 p-8", className)}>
        <div className="text-center">
          <span className="text-4xl">📄</span>
          <p className="text-sm text-gray-500 mt-2">No document available</p>
          <p className="text-xs text-gray-400 mt-1">Upload an invoice file to view it here</p>
        </div>
      </div>
    );
  }

  if (error) {
    return (
      <div className={cn("flex items-center justify-center bg-gray-50 rounded-lg border border-gray-200 p-8", className)}>
        <div className="text-center">
          <span className="text-4xl">⚠️</span>
          <p className="text-sm text-gray-500 mt-2">Could not load document</p>
          <a
            href={fileUrl}
            target="_blank"
            rel="noopener noreferrer"
            className="text-xs text-primary hover:underline mt-1 inline-block"
          >
            Open in new tab →
          </a>
        </div>
      </div>
    );
  }

  const isPDF = fileUrl.split("?")[0].toLowerCase().endsWith(".pdf") ||
    originalFilename?.toLowerCase().endsWith(".pdf");

  return (
    <div className={cn("flex flex-col rounded-lg border border-gray-200 overflow-hidden", className)}>
      {/* Toolbar */}
      <div className="flex items-center justify-between px-3 py-2 bg-gray-50 border-b border-gray-100">
        <span className="text-xs text-gray-500 truncate">
          {originalFilename || "Invoice Document"}
        </span>
        <a
          href={fileUrl}
          target="_blank"
          rel="noopener noreferrer"
          className="text-xs text-primary hover:underline"
        >
          Open in new tab ↗
        </a>
      </div>

      {/* Document viewer */}
      <div className="flex-1 bg-gray-100 min-h-[500px]">
        {isPDF ? (
          <iframe
            src={fileUrl}
            className="w-full h-full border-0"
            style={{ minHeight: "500px" }}
            title="Invoice PDF viewer"
            onError={() => setError(true)}
          />
        ) : (
          <div className="flex items-center justify-center h-full min-h-[500px]">
            {/* eslint-disable-next-line @next/next/no-img-element */}
            <img
              src={fileUrl}
              alt="Invoice document"
              className="max-w-full max-h-full object-contain"
              onError={() => setError(true)}
            />
          </div>
        )}
      </div>
    </div>
  );
}
