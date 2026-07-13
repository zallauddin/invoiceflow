/**
 * Webhooks page — manage outgoing webhook configurations.
 */
"use client";

import { useEffect, useState, useCallback } from "react";
import { webhooksApi } from "@/lib/api";
import { Card, CardHeader, CardContent, Button, Input, Badge, Spinner, EmptyState } from "@/components/ui";
import { Modal } from "@/components/ui/Modal";
import { CardSkeleton } from "@/components/ui/Skeleton";
import { useToast } from "@/components/ui/Toast";

const EVENT_LABELS: Record<string, string> = {
  "invoice.received": "📥 Invoice Received",
  "invoice.extracted": "🤖 Extraction Complete",
  "invoice.approved": "✅ Invoice Approved",
  "invoice.compliant": "🛡️ Compliance Passed",
  "invoice.transmitted": "🚀 Invoice Transmitted",
  "invoice.failed": "❌ Processing Failed",
};

export default function WebhooksPage() {
  const { toast } = useToast();
  const [webhooks, setWebhooks] = useState<Array<Record<string, unknown>>>([]);
  const [loading, setLoading] = useState(true);
  const [actionLoading, setActionLoading] = useState<string | null>(null);
  const [showCreate, setShowCreate] = useState(false);
  const [editing, setEditing] = useState<string | null>(null);
  const [newWebhook, setNewWebhook] = useState({
    name: "",
    url: "",
    secret: "",
    events: ["invoice.received"] as string[],
    active: true,
    content_type: "application/json",
    timeout_seconds: 30,
    max_retries: 3,
  });

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const data = await webhooksApi.list();
      setWebhooks(data);
    } catch { /* silent */ } finally { setLoading(false); }
  }, []);

  useEffect(() => { load(); }, [load]);

  function resetForm() {
    setNewWebhook({ name: "", url: "", secret: "", events: ["invoice.received"], active: true, content_type: "application/json", timeout_seconds: 30, max_retries: 3 });
    setEditing(null);
  }

  async function handleSave() {
    if (!newWebhook.name || !newWebhook.url) return;
    setActionLoading("save");
    try {
      if (editing) {
        await webhooksApi.update(editing!, newWebhook);
        toast("Webhook updated", "success");
      } else {
        await webhooksApi.create(newWebhook);
        toast("Webhook created", "success");
      }
      setShowCreate(false);
      resetForm();
      await load();
    } catch (e) {
      toast(e instanceof Error ? e.message : "Save failed", "error");
    } finally { setActionLoading(null); }
  }

  async function handleAction(id: string, action: string) {
    setActionLoading(`${action}-${id}`);
    try {
      switch (action) {
        case "test": await webhooksApi.test(id); break;
        case "toggle": await webhooksApi.toggle(id); break;
        case "delete": await webhooksApi.delete(id); break;
      }
      toast(`"${action}" completed`, "success");
      await load();
    } catch (e) {
      toast(e instanceof Error ? e.message : `"${action}" failed`, "error");
    } finally { setActionLoading(null); }
  }

  function openEdit(webhook: Record<string, unknown>) {
    setNewWebhook({
      name: webhook.name as string,
      url: webhook.url as string,
      secret: (webhook.secret as string) || "",
      events: webhook.events as string[],
      active: webhook.active as boolean,
      content_type: (webhook.content_type as string) || "application/json",
      timeout_seconds: (webhook.timeout_seconds as number) || 30,
      max_retries: (webhook.max_retries as number) || 3,
    });
    setEditing(webhook.id as string);
    setShowCreate(true);
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">Webhooks</h1>
          <p className="text-sm text-gray-500 mt-1">Configure outgoing webhooks for real-time event delivery</p>
        </div>
        <Button variant="primary" onClick={() => { resetForm(); setShowCreate(true); }}>🪝 Add Webhook</Button>
      </div>

      {loading ? (
        <div className="grid grid-cols-1 gap-4">
          <CardSkeleton /><CardSkeleton />
        </div>
      ) : webhooks.length === 0 ? (
        <Card>
          <EmptyState
            title="No webhooks configured"
            description="Create webhooks to receive real-time notifications when invoices are processed, approved, or transmitted."
            action={
              <Button variant="primary" onClick={() => { resetForm(); setShowCreate(true); }}>🪝 Create Your First Webhook</Button>
            }
          />
        </Card>
      ) : (
        <div className="grid grid-cols-1 gap-4">
          {webhooks.map((wh) => {
            const whId = wh.id as string;
            const loadingKey = (id: string, a: string) => actionLoading === `${a}-${id}`;

            return (
              <Card key={whId}>
                <CardHeader className="flex flex-row items-center justify-between">
                  <div>
                    <h3 className="text-sm font-semibold">{wh.name as string}</h3>
                    <p className="text-xs text-gray-500 font-mono mt-0.5 truncate max-w-md">{wh.url as string}</p>
                  </div>
                  <Badge variant={wh.active ? "success" : "muted"}>{wh.active ? "Active" : "Inactive"}</Badge>
                </CardHeader>
                <CardContent className="space-y-3">
                  {/* Events */}
                  <div>
                    <p className="text-xs text-gray-500 mb-1.5">Subscribed Events</p>
                    <div className="flex gap-1.5 flex-wrap">
                      {(wh.events as string[]).map((ev) => (
                        <Badge key={ev} variant="info">{EVENT_LABELS[ev] || ev}</Badge>
                      ))}
                    </div>
                  </div>

                  {/* Stats */}
                  <div className="grid grid-cols-3 gap-2 text-xs text-gray-500">
                    <div>Success: <span className="font-medium text-accent">{String(wh.success_count ?? 0)}</span></div>
                    <div>Failed: <span className="font-medium text-red-600">{String(wh.failure_count ?? 0)}</span></div>
                    {wh.last_status_code != null && (
                      <div>Last Code: <span className="font-medium">{String(wh.last_status_code ?? "")}</span></div>
                    )}
                  </div>
                  {wh.last_triggered_at != null && (
                    <p className="text-xs text-gray-400">Last triggered: {new Date(wh.last_triggered_at as string).toLocaleString()}</p>
                  )}

                  {/* Actions */}
                  <div className="flex gap-2 flex-wrap pt-2 border-t border-gray-50">
                    <Button variant="outline" size="sm" disabled={actionLoading !== null} onClick={() => handleAction(whId, "test")}>
                      {loadingKey(whId, "test") ? <Spinner size="sm" /> : "🧪 Test"}
                    </Button>
                    <Button variant="outline" size="sm" disabled={actionLoading !== null} onClick={() => handleAction(whId, "toggle")}>
                      {wh.active ? "⏸️ Disable" : "▶️ Enable"}
                    </Button>
                    <Button variant="ghost" size="sm" disabled={actionLoading !== null} onClick={() => openEdit(wh)}>
                      ✏️ Edit
                    </Button>
                    <Button variant="ghost" size="sm" disabled={actionLoading !== null} onClick={() => handleAction(whId, "delete")}>
                      {loadingKey(whId, "delete") ? <Spinner size="sm" /> : "🗑️"}
                    </Button>
                  </div>
                </CardContent>
              </Card>
            );
          })}
        </div>
      )}

      {/* Create/Edit Modal */}
      <Modal open={showCreate} onClose={() => { setShowCreate(false); resetForm(); }} title={editing ? "Edit Webhook" : "Add Webhook"} size="lg">
        <div className="space-y-4">
          <Input label="Name" placeholder="Production notifications" value={newWebhook.name} onChange={(e) => setNewWebhook({ ...newWebhook, name: e.target.value })} />
          <Input label="URL" placeholder="https://your-app.com/webhooks/invoiceflow" type="url" value={newWebhook.url} onChange={(e) => setNewWebhook({ ...newWebhook, url: e.target.value })} />
          <Input label="Secret (optional)" placeholder="whsec_..." type="password" value={newWebhook.secret} onChange={(e) => setNewWebhook({ ...newWebhook, secret: e.target.value })} />
          <div>
            <p className="text-sm font-medium text-gray-700 mb-2">Events</p>
            <div className="grid grid-cols-2 gap-2">
              {Object.entries(EVENT_LABELS).map(([value, label]) => (
                <label key={value} className="flex items-center gap-2 text-sm cursor-pointer hover:bg-gray-50 rounded p-1.5">
                  <input
                    type="checkbox"
                    checked={newWebhook.events.includes(value)}
                    onChange={(e) => {
                      setNewWebhook({
                        ...newWebhook,
                        events: e.target.checked
                          ? [...newWebhook.events, value]
                          : newWebhook.events.filter((ev) => ev !== value),
                      });
                    }}
                    className="rounded"
                  />
                  {label}
                </label>
              ))}
            </div>
          </div>
          <label className="flex items-center gap-2 text-sm">
            <input type="checkbox" checked={newWebhook.active} onChange={(e) => setNewWebhook({ ...newWebhook, active: e.target.checked })} className="rounded" />
            Active
          </label>
          <div className="flex justify-end gap-3 pt-2">
            <Button variant="ghost" onClick={() => { setShowCreate(false); resetForm(); }}>Cancel</Button>
            <Button variant="primary" onClick={handleSave} disabled={actionLoading === "save" || !newWebhook.name || !newWebhook.url}>
              {actionLoading === "save" ? <Spinner size="sm" /> : editing ? "Update Webhook" : "Create Webhook"}
            </Button>
          </div>
        </div>
      </Modal>
    </div>
  );
}
