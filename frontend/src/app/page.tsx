/**
 * Dashboard page — action-first triage view.
 * Shows what needs attention right now, followed by stats and charts.
 */
"use client";

import { useEffect, useState, useCallback } from "react";
import { useRouter } from "next/navigation";
import { analyticsApi, invoicesApi, complianceApi } from "@/lib/api";
import { Card, CardHeader, CardContent, Badge, Spinner, EmptyState } from "@/components/ui";
import { StatusBadge, CountryBadge } from "@/components/StatusBadge";
import { StatCardSkeleton, TableSkeleton, ChartSkeleton, Skeleton } from "@/components/ui/Skeleton";
import { useToast } from "@/components/ui/Toast";
import { formatCurrency, formatDate, timeAgo } from "@/lib/utils";
import {
  BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer,
} from "recharts";
import type { InvoiceStatus } from "@/lib/types";

const CHART_COLORS = ["#2563EB", "#16A34A", "#F59E0B", "#EF4444", "#8B5CF6", "#06B6D4"];

/* ─── Triage section ────────────────────────────────────────── */

interface TriageItem {
  label: string;
  count: number;
  icon: string;
  variant: "warning" | "danger" | "info" | "muted";
  filterKey: string;
  href: string;
}

function TriageCard({
  item,
  onClick,
}: {
  item: TriageItem;
  onClick: (item: TriageItem) => void;
}) {
  return (
    <button
      onClick={() => onClick(item)}
      className="flex items-center gap-4 bg-white rounded-xl border border-gray-200 shadow-sm p-5 text-left hover:border-primary-200 hover:shadow-md transition-all w-full"
    >
      <span className="text-2xl">{item.icon}</span>
      <div className="flex-1">
        <p className="font-semibold text-gray-900 text-2xl">{item.count}</p>
        <p className="text-sm text-gray-500">{item.label}</p>
      </div>
      <span className="text-gray-300 text-lg">→</span>
    </button>
  );
}

/* ─── Main Page ─────────────────────────────────────────────── */

export default function DashboardPage() {
  const router = useRouter();
  const { toast } = useToast();
  const [stats, setStats] = useState<{ invoices_today: number; success_rate: number; pending: number; total_processed: number } | null>(null);
  const [complianceStatus, setComplianceStatus] = useState<{ pending: number; compliant: number; failed: number } | null>(null);
  const [dailyData, setDailyData] = useState<{ labels: string[]; data: number[] }>({ labels: [], data: [] });
  const [needsReviewCount, setNeedsReviewCount] = useState(0);
  const [failedCount, setFailedCount] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    try {
      const [statsR, recentR, dailyR, compR, failedR] = await Promise.allSettled([
        analyticsApi.dashboard(),
        invoicesApi.list({ page: 1, page_size: 1, status: ["extracted", "reviewing"] as unknown as InvoiceStatus }),
        analyticsApi.daily(),
        complianceApi.status(),
        invoicesApi.list({ page: 1, page_size: 1, status: "failed" }),
      ]);

      if (statsR.status === "fulfilled") setStats(statsR.value);
      if (recentR.status === "fulfilled") setNeedsReviewCount(recentR.value.total);
      if (dailyR.status === "fulfilled") setDailyData(dailyR.value);
      if (compR.status === "fulfilled") setComplianceStatus(compR.value);
      if (failedR.status === "fulfilled") setFailedCount(failedR.value.total);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed to load dashboard");
    } finally {
      setLoading(false);
    }
  }, []);

  // Poll every 30s for fresh data
  useEffect(() => {
    load();
    const interval = setInterval(load, 30000);
    return () => clearInterval(interval);
  }, [load]);

  const handleTriageClick = (item: TriageItem) => {
    router.push(item.href);
  };

  const triageItems: TriageItem[] = [
    {
      label: "Need Review",
      count: needsReviewCount,
      icon: "👁️",
      variant: "warning",
      filterKey: "needs-review",
      href: "/invoices?filter=needs-review",
    },
    {
      label: "Failed / Errors",
      count: failedCount,
      icon: "⚠️",
      variant: "danger",
      filterKey: "failed",
      href: "/invoices?filter=failed",
    },
    {
      label: "Pending Compliance",
      count: complianceStatus?.pending ?? 0,
      icon: "🛡️",
      variant: "info",
      filterKey: "pending-compliance",
      href: "/invoices?filter=pending-approval",
    },
  ];

  // Loading state
  if (loading) {
    return (
      <div className="space-y-6">
        <Skeleton className="h-8 w-48" />
        <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
          <StatCardSkeleton />
          <StatCardSkeleton />
          <StatCardSkeleton />
        </div>
        <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
          <StatCardSkeleton />
          <StatCardSkeleton />
          <StatCardSkeleton />
          <StatCardSkeleton />
        </div>
        <ChartSkeleton height={280} />
      </div>
    );
  }

  return (
    <div className="space-y-6">
      {/* Header */}
      <div>
        <h1 className="text-2xl font-bold text-gray-900">Dashboard</h1>
        <p className="text-sm text-gray-500 mt-1">
          Overview of your e-invoice processing pipeline
        </p>
      </div>

      {error && (
        <div className="bg-red-50 border border-red-200 rounded-lg p-4 text-sm text-red-700">
          {error}
          <button onClick={() => setError(null)} className="ml-2 underline">Dismiss</button>
        </div>
      )}

      {/* ─── Triage: What needs attention? ─────────────────── */}
      <div>
        <h2 className="text-sm font-semibold text-gray-500 uppercase tracking-wider mb-3">
          ⚡ Needs Your Attention
        </h2>
        <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
          {triageItems.map((item) => (
            <TriageCard key={item.filterKey} item={item} onClick={handleTriageClick} />
          ))}
        </div>
      </div>

      {/* ─── Stats Cards ───────────────────────────────────── */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
        {[
          { label: "Invoices Today", value: stats?.invoices_today ?? 0, color: "text-primary", icon: "📄" },
          { label: "Success Rate", value: `${stats?.success_rate ?? 0}%`, color: "text-accent", icon: "✅" },
          { label: "Total Processed", value: stats?.total_processed ?? 0, color: "text-gray-800", icon: "📊" },
          { label: "Compliance Ready", value: complianceStatus?.compliant ?? 0, color: "text-accent", icon: "🛡️" },
        ].map((card) => (
          <Card key={card.label} className="p-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-sm text-gray-500">{card.label}</p>
                <p className={`text-3xl font-bold mt-1 ${card.color}`}>{card.value}</p>
              </div>
              <span className="text-2xl">{card.icon}</span>
            </div>
          </Card>
        ))}
      </div>

      {/* ─── Compliance Status Bar ──────────────────────────── */}
      {complianceStatus && (
        <Card>
          <CardHeader>
            <h3 className="text-lg font-semibold">Compliance Status</h3>
          </CardHeader>
          <CardContent>
            <div className="flex items-center gap-6 flex-wrap">
              <div className="flex items-center gap-2">
                <div className="w-3 h-3 rounded-full bg-yellow-400" />
                <span className="text-sm text-gray-600">Pending: <strong>{complianceStatus.pending}</strong></span>
              </div>
              <div className="flex items-center gap-2">
                <div className="w-3 h-3 rounded-full bg-accent" />
                <span className="text-sm text-gray-600">Compliant: <strong>{complianceStatus.compliant}</strong></span>
              </div>
              <div className="flex items-center gap-2">
                <div className="w-3 h-3 rounded-full bg-red-500" />
                <span className="text-sm text-gray-600">Failed: <strong>{complianceStatus.failed}</strong></span>
              </div>
              {complianceStatus.pending + complianceStatus.compliant + complianceStatus.failed > 0 && (
                <div className="flex-1 min-w-[200px]">
                  <div className="w-full bg-gray-100 rounded-full h-3 overflow-hidden flex">
                    <div
                      className="bg-accent h-full transition-all"
                      style={{ width: `${(complianceStatus.compliant / (complianceStatus.pending + complianceStatus.compliant + complianceStatus.failed)) * 100}%` }}
                    />
                    <div
                      className="bg-yellow-400 h-full transition-all"
                      style={{ width: `${(complianceStatus.pending / (complianceStatus.pending + complianceStatus.compliant + complianceStatus.failed)) * 100}%` }}
                    />
                    <div
                      className="bg-red-500 h-full transition-all"
                      style={{ width: `${(complianceStatus.failed / (complianceStatus.pending + complianceStatus.compliant + complianceStatus.failed)) * 100}%` }}
                    />
                  </div>
                </div>
              )}
            </div>
          </CardContent>
        </Card>
      )}

      {/* ─── Daily Volume Chart ─────────────────────────────── */}
      <Card>
        <CardHeader>
          <h3 className="text-lg font-semibold">Daily Invoice Volume</h3>
        </CardHeader>
        <CardContent>
          {dailyData.labels.length > 0 ? (
            <ResponsiveContainer width="100%" height={280}>
              <BarChart data={dailyData.labels.map((label, i) => ({ name: label, invoices: dailyData.data[i] || 0 }))}>
                <CartesianGrid strokeDasharray="3 3" stroke="#E2E8F0" />
                <XAxis dataKey="name" tick={{ fontSize: 12 }} stroke="#94A3B8" />
                <YAxis tick={{ fontSize: 12 }} stroke="#94A3B8" />
                <Tooltip contentStyle={{ borderRadius: "8px", border: "1px solid #E2E8F0" }} />
                <Bar dataKey="invoices" fill="#2563EB" radius={[4, 4, 0, 0]} />
              </BarChart>
            </ResponsiveContainer>
          ) : (
            <div className="flex items-center justify-center h-[280px] text-gray-400 text-sm">
              No data yet — process some invoices to see charts
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
