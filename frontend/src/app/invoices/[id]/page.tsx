/**
 * Invoice detail page — split-pane document viewer, field-level AI confidence,
 * progress stepper, status-driven workflow actions, toast notifications.
 */
"use client";

import { useEffect, useState, use } from "react";
import { invoicesApi, complianceApi, extractionApi } from "@/lib/api";
import type { Invoice, InvoiceLine, InvoiceStatus } from "@/lib/types";
import { Card, CardHeader, CardContent, Button, Input, Badge, Spinner, EmptyState } from "@/components/ui";
import { StatusBadge, CountryBadge, ComplianceModelBadge } from "@/components/StatusBadge";
import { DetailPageSkeleton } from "@/components/ui/Skeleton";
import { DocumentViewer } from "@/components/DocumentViewer";
import { ConfidenceField, InlineConfidence } from "@/components/ConfidenceField";
import { ProgressStepper } from "@/components/ProgressStepper";
import { useToast } from "@/components/ui/Toast";
import { formatCurrency, formatDate, timeAgo } from "@/lib/utils";

interface AuditEntry {
  action: string;
  details: Record<string, unknown>;
  timestamp: string;
  user_id: string;
}

export default function InvoiceDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const { toast } = useToast();
  const [invoice, setInvoice] = useState<Invoice | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [actionLoading, setActionLoading] = useState<string | null>(null);
  const [editing, setEditing] = useState(false);
  const [editData, setEditData] = useState<Partial<Invoice>>({});

  useEffect(() => {
    loadInvoice();
  }, [id]);

  async function loadInvoice() {
    setLoading(true);
    try {
      const data = await invoicesApi.get(id) as unknown as Invoice;
      setInvoice(data);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed to load invoice");
    } finally {
      setLoading(false);
    }
  }

  async function handleAction(action: string) {
    setActionLoading(action);
    setError(null);
    try {
      switch (action) {
        case "approve": await invoicesApi.approve(id); break;
        case "reject": await invoicesApi.reject(id); break;
        case "extract": await extractionApi.extract(id); break;
        case "validate": await complianceApi.validate(id); break;
        case "comply": await complianceApi.process(id); break;
        case "transmit": await complianceApi.transmit(id); break;
        case "archive": await complianceApi.archive(id); break;
      }
      toast(`"${action}" completed successfully`, "success");
      await loadInvoice();
    } catch (e) {
      toast(e instanceof Error ? e.message : `Action "${action}" failed`, "error");
    } finally {
      setActionLoading(null);
    }
  }

  async function handleSave() {
    setActionLoading("save");
    try {
      await invoicesApi.update(id, editData);
      toast("Invoice saved", "success");
      setEditing(false);
      setEditData({});
      await loadInvoice();
    } catch (e) {
      toast(e instanceof Error ? e.message : "Save failed", "error");
    } finally {
      setActionLoading(null);
    }
  }

  // Loading state
  if (loading) return <DetailPageSkeleton />;

  if (!invoice) {
    return (
      <div className="text-center py-16">
        <h2 className="text-xl font-bold text-gray-900">Invoice not found</h2>
        <a href="/invoices" className="text-primary hover:underline mt-2 inline-block">← Back to invoices</a>
      </div>
    );
  }

  const extracted = (invoice.extracted_data || {}) as Record<string, unknown>;
  const complianceResp = (invoice.compliance_response || {}) as Record<string, unknown>;
  const auditLog = (invoice.audit_log || []) as AuditEntry[];

  const statusActions: { action: string; label: string; variant: "primary" | "success" | "danger" | "outline" | "ghost"; requires?: string[] }[] = [
    { action: "extract", label: "🔍 Re-Extract", variant: "outline", requires: ["received", "failed"] },
    { action: "approve", label: "✅ Approve", variant: "success", requires: ["extracted", "reviewing"] },
    { action: "reject", label: "❌ Reject", variant: "danger", requires: ["extracted", "reviewing"] },
    { action: "validate", label: "🔬 Validate", variant: "outline", requires: ["approved"] },
    { action: "comply", label: "📤 Process Compliance", variant: "primary", requires: ["approved"] },
    { action: "transmit", label: "🚀 Transmit", variant: "success", requires: ["compliant"] },
    { action: "archive", label: "🗄️ Archive", variant: "outline", requires: ["compliant", "transmitted"] },
  ];

  const availableActions = statusActions.filter((a) => !a.requires || a.requires.includes(invoice.status));

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-start justify-between">
        <div>
          <a href="/invoices" className="text-sm text-gray-500 hover:text-primary">← Back to Invoices</a>
          <div className="flex items-center gap-3 mt-2">
            <h1 className="text-2xl font-bold text-gray-900">{invoice.invoice_number || "Untitled Invoice"}</h1>
            <StatusBadge status={invoice.status as InvoiceStatus} />
          </div>
          <div className="flex items-center gap-3 mt-2">
            <CountryBadge code={invoice.country_code} />
            <ComplianceModelBadge model={invoice.compliance_model} />
            <Badge variant="muted">{invoice.source}</Badge>
            {invoice.ocr_confidence != null && invoice.ocr_confidence > 0 && (
              <InlineConfidence confidence={invoice.ocr_confidence} />
            )}
          </div>
        </div>
        <div className="flex items-center gap-2 flex-wrap justify-end">
          {availableActions.map((a) => (
            <Button key={a.action} variant={a.variant} size="sm" disabled={actionLoading !== null} onClick={() => handleAction(a.action)}>
              {actionLoading === a.action ? <Spinner size="sm" /> : a.label}
            </Button>
          ))}
        </div>
      </div>

      {/* Progress Stepper */}
      <Card>
        <CardContent className="py-4">
          <ProgressStepper status={invoice.status as InvoiceStatus} />
        </CardContent>
      </Card>

      {error && (
        <div className="bg-red-50 border border-red-200 rounded-lg p-4 text-sm text-red-700">
          {error}
          <button onClick={() => setError(null)} className="ml-2 underline">Dismiss</button>
        </div>
      )}

      {/* ─── Split-pane: Document + Data ──────────────────── */}
      <div className="grid grid-cols-1 xl:grid-cols-2 gap-6">
        {/* Left: Document Viewer */}
        <DocumentViewer
          fileUrl={invoice.file_url}
          originalFilename={invoice.original_filename}
          className="h-[600px] xl:h-auto"
        />

        {/* Right: Invoice Details */}
        <div className="space-y-6">
          {/* Basic Info with confidence */}
          <Card>
            <CardHeader className="flex flex-row items-center justify-between">
              <h3 className="text-lg font-semibold">Invoice Details</h3>
              {!editing ? (
                <Button variant="ghost" size="sm" onClick={() => { setEditing(true); setEditData(invoice); }}>✏️ Edit</Button>
              ) : (
                <div className="flex gap-2">
                  <Button variant="ghost" size="sm" onClick={() => { setEditing(false); setEditData({}); }}>Cancel</Button>
                  <Button variant="primary" size="sm" disabled={actionLoading === "save"} onClick={handleSave}>
                    {actionLoading === "save" ? <Spinner size="sm" /> : "Save"}
                  </Button>
                </div>
              )}
            </CardHeader>
            <CardContent>
              {editing ? (
                <div className="grid grid-cols-2 gap-4">
                  <DetailField label="Invoice Number">
                    <Input value={editData.invoice_number || ""} onChange={(e) => setEditData({ ...editData, invoice_number: e.target.value })} />
                  </DetailField>
                  <DetailField label="Invoice Date">
                    <Input type="date" value={editData.invoice_date || ""} onChange={(e) => setEditData({ ...editData, invoice_date: e.target.value })} />
                  </DetailField>
                  <DetailField label="Due Date">
                    <Input type="date" value={editData.due_date || ""} onChange={(e) => setEditData({ ...editData, due_date: e.target.value })} />
                  </DetailField>
                  <DetailField label="Currency">
                    <Input value={editData.currency || ""} onChange={(e) => setEditData({ ...editData, currency: e.target.value })} />
                  </DetailField>
                  <DetailField label="Vendor Name">
                    <Input value={editData.vendor_name || ""} onChange={(e) => setEditData({ ...editData, vendor_name: e.target.value })} />
                  </DetailField>
                </div>
              ) : (
                <div className="grid grid-cols-2 gap-3">
                  <ConfidenceField label="Invoice Number" value={invoice.invoice_number} />
                  <ConfidenceField label="Invoice Date" value={formatDate(invoice.invoice_date ?? "")} />
                  <ConfidenceField label="Due Date" value={formatDate(invoice.due_date ?? "")} />
                  <ConfidenceField label="Currency" value={invoice.currency} />
                  <ConfidenceField label="Vendor Name" value={invoice.vendor_name || "—"} />
                  <ConfidenceField label="Vendor Tax ID" value={invoice.vendor_tax_id || "—"} />
                  <ConfidenceField label="Buyer Name" value={invoice.buyer_name || "—"} />
                  <ConfidenceField label="Buyer Tax ID" value={invoice.buyer_tax_id || "—"} />
                </div>
              )}
            </CardContent>
          </Card>

          {/* Totals */}
          <Card>
            <CardHeader><h3 className="text-lg font-semibold">Totals</h3></CardHeader>
            <CardContent className="space-y-3">
              <div className="flex justify-between text-sm"><span className="text-gray-500">Subtotal</span><span className="font-medium">{formatCurrency(invoice.subtotal || 0, invoice.currency)}</span></div>
              <div className="flex justify-between text-sm"><span className="text-gray-500">Tax</span><span className="font-medium">{formatCurrency(invoice.tax_amount || 0, invoice.currency)}</span></div>
              <div className="border-t border-gray-100 pt-3 flex justify-between"><span className="font-semibold text-gray-900">Total</span><span className="font-bold text-lg text-gray-900">{formatCurrency(invoice.total_amount || 0, invoice.currency)}</span></div>
            </CardContent>
          </Card>
        </div>
      </div>

      {/* Line Items */}
      <Card>
        <CardHeader><h3 className="text-lg font-semibold">Line Items</h3></CardHeader>
        <CardContent>
          {invoice.lines && (invoice.lines as InvoiceLine[]).length > 0 ? (
            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <thead>
                  <tr className="border-b border-gray-100">
                    <th className="text-left py-2 text-xs font-medium text-gray-500">#</th>
                    <th className="text-left py-2 text-xs font-medium text-gray-500">Description</th>
                    <th className="text-right py-2 text-xs font-medium text-gray-500">Qty</th>
                    <th className="text-right py-2 text-xs font-medium text-gray-500">Unit Price</th>
                    <th className="text-right py-2 text-xs font-medium text-gray-500">Tax %</th>
                    <th className="text-right py-2 text-xs font-medium text-gray-500">Total</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-50">
                  {(invoice.lines as InvoiceLine[]).map((line, i) => (
                    <tr key={line.id || i}>
                      <td className="py-2 text-gray-500">{line.line_number || i + 1}</td>
                      <td className="py-2 text-gray-900">{line.description}</td>
                      <td className="py-2 text-right text-gray-700">{line.quantity}</td>
                      <td className="py-2 text-right text-gray-700">{formatCurrency(line.unit_price, invoice.currency)}</td>
                      <td className="py-2 text-right text-gray-700">{line.tax_rate}%</td>
                      <td className="py-2 text-right font-medium text-gray-900">{formatCurrency(line.line_total, invoice.currency)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : (
            <p className="text-sm text-gray-500 text-center py-4">No line items — run extraction to populate</p>
          )}
        </CardContent>
      </Card>

      {/* AI Extracted Data */}
      {Object.keys(extracted).length > 0 && (
        <Card>
          <CardHeader><h3 className="text-lg font-semibold">🤖 AI Extracted Data</h3></CardHeader>
          <CardContent>
            <div className="grid grid-cols-2 gap-3">
              {Object.entries(extracted).map(([key, value]) => (
                <div key={key} className="bg-gray-50 rounded-lg p-3">
                  <p className="text-xs text-gray-500 uppercase">{key.replace(/_/g, " ")}</p>
                  <p className="text-sm font-medium text-gray-900 mt-0.5">
                    {typeof value === "object" ? JSON.stringify(value) : String(value ?? "—")}
                  </p>
                </div>
              ))}
            </div>
          </CardContent>
        </Card>
      )}

      {/* Compliance Response + Audit Trail */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        {Object.keys(complianceResp).length > 0 && (
          <Card>
            <CardHeader><h3 className="text-lg font-semibold">Compliance Response</h3></CardHeader>
            <CardContent>
              <div className="space-y-2">
                {Object.entries(complianceResp).map(([key, value]) => (
                  <div key={key} className="flex justify-between text-sm">
                    <span className="text-gray-500 truncate">{key.replace(/_/g, " ")}</span>
                    <span className="font-medium text-right ml-2 truncate max-w-[160px]">
                      {typeof value === "object" ? JSON.stringify(value) : String(value ?? "—")}
                    </span>
                  </div>
                ))}
              </div>
            </CardContent>
          </Card>
        )}

        <Card>
          <CardHeader><h3 className="text-lg font-semibold">Audit Trail</h3></CardHeader>
          <CardContent>
            {auditLog.length > 0 ? (
              <div className="space-y-3">
                {auditLog.slice(0, 10).map((entry, i) => (
                  <div key={i} className="text-sm">
                    <div className="flex items-center gap-2">
                      <div className="w-2 h-2 rounded-full bg-primary shrink-0" />
                      <span className="font-medium text-gray-900">{entry.action}</span>
                    </div>
                    <p className="text-xs text-gray-500 ml-4 mt-0.5">{timeAgo(entry.timestamp)}</p>
                  </div>
                ))}
              </div>
            ) : (
              <p className="text-sm text-gray-500 text-center py-4">No audit entries yet</p>
            )}
          </CardContent>
        </Card>
      </div>

      {/* Metadata */}
      <Card>
        <CardHeader><h3 className="text-lg font-semibold">Metadata</h3></CardHeader>
        <CardContent className="grid grid-cols-2 md:grid-cols-4 gap-4 text-sm">
          <div><span className="text-gray-500">Created</span><p>{formatDate(invoice.created_at)}</p></div>
          <div><span className="text-gray-500">Updated</span><p>{formatDate(invoice.updated_at)}</p></div>
          <div><span className="text-gray-500">Extraction</span><Badge variant="muted">{invoice.extraction_method || "none"}</Badge></div>
          {invoice.file_url && (
            <div><span className="text-gray-500">File</span><a href={invoice.file_url} target="_blank" rel="noopener noreferrer" className="text-primary hover:underline">View file →</a></div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}

/* ─── Helper ────────────────────────────────────────────────── */

function DetailField({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div>
      <p className="text-xs text-gray-500 uppercase tracking-wide">{label}</p>
      <div className="text-sm font-medium text-gray-900 mt-0.5">{children}</div>
    </div>
  );
}
