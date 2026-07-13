/**
 * Toast notification system — context-based, supports success/error/info/warning.
 *
 * Usage:
 *   Wrap your app with <ToastProvider> in layout.
 *   Call const { toast } = useToast() in any component.
 *   toast("Invoice saved", "success")
 */

"use client";

import { createContext, useContext, useState, useCallback, type ReactNode } from "react";
import { cn } from "@/lib/utils";

/* ─── Types ─────────────────────────────────────────────────── */

type ToastVariant = "success" | "error" | "info" | "warning";

interface Toast {
  id: number;
  message: string;
  variant: ToastVariant;
  exiting: boolean;
}

interface ToastContextValue {
  toast: (message: string, variant?: ToastVariant) => void;
}

/* ─── Context ───────────────────────────────────────────────── */

const ToastContext = createContext<ToastContextValue | null>(null);

export function useToast() {
  const ctx = useContext(ToastContext);
  if (!ctx) throw new Error("useToast must be used within <ToastProvider>");
  return ctx;
}

/* ─── Provider ──────────────────────────────────────────────── */

let nextId = 0;

export function ToastProvider({ children }: { children: ReactNode }) {
  const [toasts, setToasts] = useState<Toast[]>([]);

  const toast = useCallback((message: string, variant: ToastVariant = "info") => {
    const id = nextId++;
    setToasts((prev) => [...prev, { id, message, variant, exiting: false }]);

    // Auto-dismiss after 4s
    setTimeout(() => {
      // Mark as exiting for fade-out animation
      setToasts((prev) =>
        prev.map((t) => (t.id === id ? { ...t, exiting: true } : t)),
      );
      // Remove after animation completes
      setTimeout(() => {
        setToasts((prev) => prev.filter((t) => t.id !== id));
      }, 300);
    }, 4000);
  }, []);

  return (
    <ToastContext.Provider value={{ toast }}>
      {children}
      {/* Toast container — fixed bottom-right */}
      <div className="fixed bottom-4 right-4 z-50 flex flex-col gap-2 pointer-events-none">
        {toasts.map((t) => (
          <ToastItem key={t.id} toast={t} />
        ))}
      </div>
    </ToastContext.Provider>
  );
}

/* ─── Toast Item ────────────────────────────────────────────── */

const variantStyles: Record<ToastVariant, { bg: string; border: string; icon: string }> = {
  success: { bg: "bg-accent-50", border: "border-accent-200", icon: "✅" },
  error: { bg: "bg-red-50", border: "border-red-200", icon: "❌" },
  info: { bg: "bg-blue-50", border: "border-blue-200", icon: "ℹ️" },
  warning: { bg: "bg-yellow-50", border: "border-yellow-200", icon: "⚠️" },
};

function ToastItem({ toast: t }: { toast: Toast }) {
  const style = variantStyles[t.variant];

  return (
    <div
      className={cn(
        "pointer-events-auto flex items-center gap-2 px-4 py-3 rounded-lg border shadow-lg text-sm font-medium",
        "transition-all duration-300",
        style.bg,
        style.border,
        t.exiting ? "opacity-0 translate-x-4" : "opacity-100 translate-x-0",
      )}
    >
      <span>{style.icon}</span>
      <span className="text-gray-800">{t.message}</span>
    </div>
  );
}
