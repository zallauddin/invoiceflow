/**
 * Invoices list page — smart filters, bulk actions, skeleton loading, real-time polling.
 */
"use client";

import { useEffect, useState, useCallback, useRef } from "react";
import { invoicesApi, ingestionApi, InvoiceListParams } from "@/lib/api";
import type { Invoice, InvoiceStatus } from "@/lib/types";
import { Card, CardContent, Button, Input, Select, Badge, Spinner, EmptyState } from "@/components/ui";
import { StatusBadge, CountryBadge, ComplianceModelBadge } from "@/components/StatusBadge";
import { TableSkeleton, StatCardSkeleton } from "@/components/ui/Skeleton";
import { SmartFilters, getStatusesForFilter, SMART_FILTERS } from "@/components/SmartFilters";
import { BulkActionBar } from "@/components/BulkActionBar";
import { useToast } from "@/components/ui/Toast";
import { formatCurrency, formatDate } from "@/lib/utils";

const STATUS_OPTIONS = [
  { value: "", label: "All Statuses" },
  { value: "received", label: "Received" },
  { value: "processing", label: "Processing" },
  { value: "extracted", label: "Extracted" },
  { value: "reviewing", label: "Reviewing" },
  { value: "approved", label: "Approved" },
  { value: "compliant", label: "Compliant" },
  { value: "transmitted", label: "Transmitted" },
  { value: "failed", label: "Failed" },
  { value: "rejected", label: "Rejected" },
];

const COUNTRY_OPTIONS = [
  { value: "", label: "All Countries" },
  { value: "SA", label: "🇸🇦 Saudi Arabia" },
  { value: "BR", label: "🇧🇷 Brazil" },
  { value: "IN", label: "🇮🇳 India" },
  { value: "MX", label: "🇲🇽 Mexico" },
  { value: "DE", label: "🇩🇪 Germany" },
  { value: "FR", label: "🇫🇷 France" },
  { value: "IT", label: "🇮🇹 Italy" },
  { value: "PL", label: "🇵🇱 Poland" },
  { value: "US", label: "🇺🇸 United States" },
  { value: "GB", label: "🇬🇧 United Kingdom" },
];

export default function InvoicesPage() {
  const { toast } = useToast();
  const [invoices, setInvoices] = useState<Invoice[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [pageSize] = useState(20);
  const [statusFilter, setStatusFilter] = useState("");
  const [countryFilter, setCountryFilter] = useState("");
  const [searchQuery, setSearchQuery] = useState("");
  const [activeFilter, setActiveFilter] = useState("all");
  const [debouncedSearch, setDebouncedSearch] = useState("");
  const debounceRef = useRef<ReturnType<typeof setTimeout>>();
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());
  const [loading, setLoading] = useState(true);
  const [uploading, setUploading] = useState(false);
  const [bulkLoading, setBulkLoading] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  // Debounce search input (300ms)
  const handleSearchChange = (value: string) => {
    setSearchQuery(value);
    if (debounceRef.current) clearTimeout(debounceRef.current);
    debounceRef.current = setTimeout(() => {
      setDebouncedSearch(value);
      setPage(1);
    }, 300);
  };

  const loadInvoices = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const statuseValues = getStatusesForFilter(activeFilter);
      // For smart filters with multi-status values, send the first one to the API
      // to get a reasonable page; client-side filter handles the rest
      const smartStatus = statuseValues && statuseValues.length > 0 ? statuseValues[0] : undefined;
      const params: InvoiceListParams = {
        page,
        page_size: pageSize,
        ...(statusFilter && { status: statusFilter as InvoiceStatus }),
        ...(countryFilter && { country: countryFilter }),
        ...(debouncedSearch && { search: debouncedSearch }),
        ...(smartStatus && { status: smartStatus }),
      };
      const data = await invoicesApi.list(params);
      // Client-side filter for multi-status smart filters
      let filtered = data.invoices;
      if (statuseValues && statuseValues.length > 1) {
        filtered = data.invoices.filter((inv) => statuseValues.includes(inv.status as InvoiceStatus));
      }
      setInvoices(filtered);
      setTotal(data.total);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed to load invoices");
    } finally {
      setLoading(false);
    }
  }, [page, pageSize, statusFilter, countryFilter, debouncedSearch, activeFilter]);

  // Load + poll every 30s (use ref to avoid stale closure)
  const loadInvoicesRef = useRef(loadInvoices);
  loadInvoicesRef.current = loadInvoices;

  useEffect(() => { loadInvoices(); }, [loadInvoices]);
  useEffect(() => {
    const interval = setInterval(() => loadInvoicesRef.current(), 30000);
    return () => clearInterval(interval);
  }, []);

  // Reset page on filter change
  const handleFilterChange = (key: string) => {
    setActiveFilter(key);
    setPage(1);
    setSelectedIds(new Set());
    if (key === "all") {
      setStatusFilter("");
    }
  };

  const handleFileUpload = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;
    setUploading(true);
    try {
      const result = await ingestionApi.upload(file);
      toast(`File "${file.name}" uploaded and queued for processing`, "success");
      setTimeout(loadInvoices, 2000);
    } catch (err) {
      toast(err instanceof Error ? err.message : "Upload failed", "error");
    } finally {
      setUploading(false);
      e.target.value = "";
    }
  };

  /* ─── Bulk actions ──────────────────────────────────────── */

  const toggleSelect = (id: string) => {
    setSelectedIds((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id); else next.add(id);
      return next;
    });
  };

  const toggleSelectAll = () => {
    if (selectedIds.size === invoices.length) {
      setSelectedIds(new Set());
    } else {
      setSelectedIds(new Set(invoices.map((inv) => inv.id)));
    }
  };

  async function handleBulkAction(action: string) {
    setBulkLoading(action);
    try {
      const ids = Array.from(selectedIds);
      for (const id of ids) {
        switch (action) {
          case "approve": await invoicesApi.approve(id); break;
          case "reject": await invoicesApi.reject(id); break;
        }
      }
      toast(`${action} completed for ${ids.length} invoice(s)`, "success");
      setSelectedIds(new Set());
      await loadInvoices();
    } catch (e) {
      toast(e instanceof Error ? e.message : `Bulk ${action} failed`, "error");
    } finally {
      setBulkLoading(null);
    }
  }

  const totalPages = Math.ceil(total / pageSize);

  return (
    <div className="space-y-4">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">Invoices</h1>
          <p className="text-sm text-gray-500 mt-1">{total} total invoices</p>
        </div>
        <div className="flex items-center gap-3">
          <label className="cursor-pointer">
            <input
              type="file"
              accept=".pdf,.xml,.png,.jpg,.jpeg,.tiff"
              className="hidden"
              onChange={handleFileUpload}
            />
            <Button variant="primary" disabled={uploading}>
              {uploading ? <><Spinner size="sm" /> Uploading...</> : <>📤 Upload Invoice</>}
            </Button>
          </label>
        </div>
      </div>

      {/* Smart Filters */}
      <SmartFilters activeFilter={activeFilter} onFilterChange={handleFilterChange} />

      {error && (
        <div className="bg-red-50 border border-red-200 rounded-lg p-4 text-sm text-red-700">
          {error}
          <button onClick={() => setError(null)} className="ml-2 underline">Dismiss</button>
        </div>
      )}

      {/* Bulk Action Bar */}
      <BulkActionBar
        selectedCount={selectedIds.size}
        loading={bulkLoading}
        onApprove={() => handleBulkAction("approve")}
        onReject={() => handleBulkAction("reject")}
        onClear={() => setSelectedIds(new Set())}
      />

      {/* Filters */}
      <Card>
        <CardContent className="py-3">
          <div className="flex items-center gap-4 flex-wrap">
            <div className="flex-1 min-w-[200px]">
              <Input
                placeholder="Search by invoice number, vendor..."
                value={searchQuery}
                onChange={(e) => handleSearchChange(e.target.value)}
              />
            </div>
            <div className="w-44">
              <Select
                options={STATUS_OPTIONS}
                value={statusFilter}
                onChange={(e) => { setStatusFilter(e.target.value); setPage(1); }}
              />
            </div>
            <div className="w-44">
              <Select
                options={COUNTRY_OPTIONS}
                value={countryFilter}
                onChange={(e) => { setCountryFilter(e.target.value); setPage(1); }}
              />
            </div>
            {(statusFilter || countryFilter || searchQuery) && (
              <Button variant="ghost" size="sm" onClick={() => { setStatusFilter(""); setCountryFilter(""); setSearchQuery(""); setPage(1); }}>
                Clear filters
              </Button>
            )}
          </div>
        </CardContent>
      </Card>

      {/* Table */}
      <Card>
        {loading ? (
          <div className="p-6">
            <TableSkeleton rows={8} cols={8} />
          </div>
        ) : invoices.length === 0 ? (
          <EmptyState
            title="No invoices found"
            description="Try adjusting your filters or upload a new invoice."
          />
        ) : (
          <>
            <div className="overflow-x-auto">
              <table className="w-full">
                <thead>
                  <tr className="border-b border-gray-100">
                    <th className="text-left px-4 py-3 w-10">
                      <input
                        type="checkbox"
                        checked={selectedIds.size === invoices.length && invoices.length > 0}
                        onChange={toggleSelectAll}
                        className="rounded border-gray-300 text-primary focus:ring-primary/50"
                      />
                    </th>
                    <th className="text-left px-6 py-3 text-xs font-medium text-gray-500 uppercase tracking-wider">Invoice</th>
                    <th className="text-left px-6 py-3 text-xs font-medium text-gray-500 uppercase tracking-wider">Vendor</th>
                    <th className="text-left px-6 py-3 text-xs font-medium text-gray-500 uppercase tracking-wider">Status</th>
                    <th className="text-left px-6 py-3 text-xs font-medium text-gray-500 uppercase tracking-wider">Country</th>
                    <th className="text-left px-6 py-3 text-xs font-medium text-gray-500 uppercase tracking-wider">Model</th>
                    <th className="text-right px-6 py-3 text-xs font-medium text-gray-500 uppercase tracking-wider">Amount</th>
                    <th className="text-left px-6 py-3 text-xs font-medium text-gray-500 uppercase tracking-wider">Date</th>
                    <th className="text-left px-6 py-3 text-xs font-medium text-gray-500 uppercase tracking-wider">Source</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-50">
                  {invoices.map((inv) => (
                    <tr key={inv.id} className="hover:bg-gray-50 transition-colors">
                      <td className="px-4 py-3">
                        <input
                          type="checkbox"
                          checked={selectedIds.has(inv.id)}
                          onChange={() => toggleSelect(inv.id)}
                          className="rounded border-gray-300 text-primary focus:ring-primary/50"
                        />
                      </td>
                      <td className="px-6 py-3">
                        <a href={`/invoices/${inv.id}`} className="text-sm font-medium text-primary hover:underline">
                          {inv.invoice_number || "—"}
                        </a>
                        <p className="text-xs text-gray-400 font-mono mt-0.5">{(inv.id as string).slice(0, 8)}...</p>
                      </td>
                      <td className="px-6 py-3"><span className="text-sm text-gray-700">{inv.vendor_name || "—"}</span></td>
                      <td className="px-6 py-3"><StatusBadge status={inv.status as InvoiceStatus} /></td>
                      <td className="px-6 py-3"><CountryBadge code={inv.country_code || "US"} /></td>
                      <td className="px-6 py-3"><ComplianceModelBadge model={inv.compliance_model || "peppol"} /></td>
                      <td className="px-6 py-3 text-right">
                        <span className="text-sm font-medium text-gray-900">{formatCurrency(inv.total_amount || 0, inv.currency)}</span>
                      </td>
                      <td className="px-6 py-3"><span className="text-sm text-gray-500">{formatDate(inv.invoice_date || inv.created_at)}</span></td>
                      <td className="px-6 py-3"><Badge variant="muted">{inv.source || "manual"}</Badge></td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            {/* Pagination */}
            {totalPages > 1 && (
              <div className="flex items-center justify-between px-6 py-3 border-t border-gray-100">
                <p className="text-sm text-gray-500">
                  Showing {(page - 1) * pageSize + 1}–{Math.min(page * pageSize, total)} of {total}
                </p>
                <div className="flex items-center gap-2">
                  <Button variant="outline" size="sm" disabled={page <= 1} onClick={() => setPage(page - 1)}>← Previous</Button>
                  <span className="text-sm text-gray-600">Page {page} of {totalPages}</span>
                  <Button variant="outline" size="sm" disabled={page >= totalPages} onClick={() => setPage(page + 1)}>Next →</Button>
                </div>
              </div>
            )}
          </>
        )}
      </Card>
    </div>
  );
}
