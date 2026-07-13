/**
 * Connectors page — manage ERP integrations (Xero, SAP, Oracle).
 */
"use client";

import { useEffect, useState, useCallback } from "react";
import { connectorsApi } from "@/lib/api";
import { Card, CardHeader, CardContent, Button, Input, Select, Badge, Spinner, EmptyState } from "@/components/ui";
import { Modal } from "@/components/ui/Modal";
import { CardSkeleton } from "@/components/ui/Skeleton";
import { useToast } from "@/components/ui/Toast";

const CONNECTOR_TYPES = [
  { value: "sap", label: "SAP" },
  { value: "oracle", label: "Oracle" },
  { value: "xero", label: "Xero" },
];

const DIRECTION_LABELS: Record<string, string> = {
  push: "Push →",
  pull: "← Pull",
  bidirectional: "↔ Bidirectional",
};

const STATUS_LABELS: Record<string, { label: string; variant: "success" | "warning" | "danger" | "info" | "muted" }> = {
  active: { label: "Active", variant: "success" },
  inactive: { label: "Inactive", variant: "muted" },
  error: { label: "Error", variant: "danger" },
  pending_auth: { label: "Pending Auth", variant: "warning" },
};

export default function ConnectorsPage() {
  const { toast } = useToast();
  const [connectors, setConnectors] = useState<Array<Record<string, unknown>>>([]);
  const [available, setAvailable] = useState<Array<Record<string, unknown>>>([]);
  const [loading, setLoading] = useState(true);
  const [actionLoading, setActionLoading] = useState<string | null>(null);
  const [showCreate, setShowCreate] = useState(false);
  const [newConnector, setNewConnector] = useState({
    connector_type: "xero" as "sap" | "oracle" | "xero",
    display_name: "",
    api_key: "",
    base_url: "",
    sandbox: true,
    sync_direction: "pull" as "push" | "pull" | "bidirectional",
  });

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const [configs, avail] = await Promise.allSettled([
        connectorsApi.list(),
        connectorsApi.listAvailable(),
      ]);
      if (configs.status === "fulfilled") setConnectors(configs.value);
      if (avail.status === "fulfilled") setAvailable(avail.value);
    } catch { /* silent */ } finally { setLoading(false); }
  }, []);

  useEffect(() => { load(); }, [load]);

  async function handleCreate() {
    if (!newConnector.display_name) return;
    setActionLoading("create");
    try {
      await connectorsApi.create(newConnector);
      toast(`Connector "${newConnector.display_name}" created`, "success");
      setShowCreate(false);
      setNewConnector({ connector_type: "xero" as "sap" | "oracle" | "xero", display_name: "", api_key: "", base_url: "", sandbox: true, sync_direction: "pull" as "push" | "pull" | "bidirectional" });
      await load();
    } catch (e) {
      toast(e instanceof Error ? e.message : "Creation failed", "error");
    } finally { setActionLoading(null); }
  }

  async function handleAction(id: string, action: string) {
    setActionLoading(`${action}-${id}`);
    try {
      switch (action) {
        case "test": await connectorsApi.test(id); break;
        case "sync": await connectorsApi.sync(id, { direction: "pull" as const, limit: 100 }); break;
        case "activate": await connectorsApi.activate(id); break;
        case "deactivate": await connectorsApi.deactivate(id); break;
        case "delete": await connectorsApi.delete(id); break;
      }
      toast(`"${action}" completed`, "success");
      await load();
    } catch (e) {
      toast(e instanceof Error ? e.message : `"${action}" failed`, "error");
    } finally { setActionLoading(null); }
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">ERP Connectors</h1>
          <p className="text-sm text-gray-500 mt-1">Manage integrations with external ERP systems</p>
        </div>
        <Button variant="primary" onClick={() => setShowCreate(true)}>🔌 Add Connector</Button>
      </div>

      {loading ? (
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          <CardSkeleton /><CardSkeleton />
        </div>
      ) : connectors.length === 0 ? (
        <Card>
          <EmptyState
            title="No connectors configured"
            description="Connect to Xero, SAP, or Oracle to sync invoices bidirectionally."
            action={
              <Button variant="primary" onClick={() => setShowCreate(true)}>🔌 Add Your First Connector</Button>
            }
          />
        </Card>
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          {connectors.map((conn) => {
            const status = STATUS_LABELS[conn.status as string] || { label: conn.status, variant: "muted" as const };
            const loadingKey = (id: string, a: string) => actionLoading === `${a}-${id}`;
            const connId = conn.id as string;

            return (
              <Card key={connId}>
                <CardHeader className="flex flex-row items-center justify-between">
                  <div className="flex items-center gap-2">
                    <span className="text-xl">
                      {conn.connector_type === "xero" ? "💚" : conn.connector_type === "sap" ? "🔷" : "🔴"}
                    </span>
                    <div>
                      <h3 className="text-sm font-semibold">{conn.display_name as string}</h3>
                      <p className="text-xs text-gray-500">{CONNECTOR_TYPES.find(c => c.value === conn.connector_type)?.label || String(conn.connector_type ?? "")}</p>
                    </div>
                  </div>
                  <Badge variant={status.variant}>{status.label}</Badge>
                </CardHeader>
                <CardContent className="space-y-3">
                  {/* Info */}
                  <div className="grid grid-cols-2 gap-2 text-xs">
                    <div><span className="text-gray-500">Direction:</span> <span className="font-medium">{DIRECTION_LABELS[conn.sync_direction as string] || String(conn.sync_direction ?? "")}</span></div>
                    <div><span className="text-gray-500">Mode:</span> <span className="font-medium">{conn.sandbox ? "Sandbox" : "Production"}</span></div>
                    {conn.last_sync_at != null && (
                      <div className="col-span-2"><span className="text-gray-500">Last sync:</span> <span className="font-medium">{new Date(conn.last_sync_at as string).toLocaleDateString()}</span></div>
                    )}
                    {(conn.total_synced !== undefined || conn.total_failed !== undefined) && (
                      <div className="col-span-2">
                        <span className="text-gray-500">Synced:</span>                    <span className="font-medium text-accent">{String(conn.total_synced ?? 0)}</span>
                        <span className="text-gray-400 mx-1">|</span>
                        <span className="text-gray-500">Failed:</span>                    <span className="font-medium text-red-600">{String(conn.total_failed ?? 0)}</span>
                      </div>
                    )}
                  </div>

                  {/* Actions */}
                  <div className="flex gap-2 flex-wrap pt-2 border-t border-gray-50">
                    <Button variant="outline" size="sm" disabled={actionLoading !== null} onClick={() => handleAction(connId, "test")}>
                      {loadingKey(connId, "test") ? <Spinner size="sm" /> : "🧪 Test"}
                    </Button>
                    <Button variant="outline" size="sm" disabled={actionLoading !== null} onClick={() => handleAction(connId, "sync")}>
                      {loadingKey(connId, "sync") ? <Spinner size="sm" /> : "🔄 Sync"}
                    </Button>
                    {conn.status === "active" ? (
                      <Button variant="ghost" size="sm" disabled={actionLoading !== null} onClick={() => handleAction(connId, "deactivate")}>
                        ⏸️ Deactivate
                      </Button>
                    ) : (
                      <Button variant="ghost" size="sm" disabled={actionLoading !== null} onClick={() => handleAction(connId, "activate")}>
                        ▶️ Activate
                      </Button>
                    )}
                    <Button variant="ghost" size="sm" disabled={actionLoading !== null} onClick={() => handleAction(connId, "delete")}>
                      {loadingKey(connId, "delete") ? <Spinner size="sm" /> : "🗑️"}
                    </Button>
                  </div>
                </CardContent>
              </Card>
            );
          })}
        </div>
      )}

      {/* Available connector types */}
      {available.length > 0 && (
        <Card>
          <CardHeader><h3 className="text-lg font-semibold">Available ERP Integrations</h3></CardHeader>
          <CardContent>
            <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
              {available.map((a, i) => (
                <div key={i} className="border border-gray-200 rounded-lg p-4 text-center">
                  <span className="text-2xl">
                    {a.type === "xero" ? "💚" : a.type === "sap" ? "🔷" : "🔴"}
                  </span>
                  <p className="text-sm font-medium mt-2">{a.display_name as string}</p>
                  <p className="text-xs text-gray-500 mt-1">                        {(a.directions as string[])?.map((d: string) => DIRECTION_LABELS[d] || d).join(", ") || "—"}

                  </p>
                </div>
              ))}
            </div>
          </CardContent>
        </Card>
      )}

      {/* Create Modal */}
      <Modal open={showCreate} onClose={() => setShowCreate(false)} title="Add ERP Connector" size="lg">
        <div className="space-y-4">
          <Select
            label="ERP System"
            value={newConnector.connector_type}
            onChange={(e) => setNewConnector({ ...newConnector, connector_type: e.target.value as "sap" | "oracle" | "xero" })}
            options={CONNECTOR_TYPES}
          />
          <Input label="Display Name" placeholder="Production Xero" value={newConnector.display_name} onChange={(e) => setNewConnector({ ...newConnector, display_name: e.target.value })} />
          <Input label="API Key" placeholder="sk_..." value={newConnector.api_key} onChange={(e) => setNewConnector({ ...newConnector, api_key: e.target.value })} />
          <Input label="Base URL" placeholder="https://api.xero.com" value={newConnector.base_url} onChange={(e) => setNewConnector({ ...newConnector, base_url: e.target.value })} />
          <Select
            label="Sync Direction"
            value={newConnector.sync_direction}
            onChange={(e) => setNewConnector({ ...newConnector, sync_direction: e.target.value as "push" | "pull" | "bidirectional" })}
            options={[
              { value: "push", label: "Push (send invoices to ERP)" },
              { value: "pull", label: "Pull (import from ERP)" },
              { value: "bidirectional", label: "Bidirectional" },
            ]}
          />
          <label className="flex items-center gap-2 text-sm">
            <input type="checkbox" checked={newConnector.sandbox} onChange={(e) => setNewConnector({ ...newConnector, sandbox: e.target.checked })} className="rounded" />
            Sandbox mode
          </label>
          <div className="flex justify-end gap-3 pt-2">
            <Button variant="ghost" onClick={() => setShowCreate(false)}>Cancel</Button>
            <Button variant="primary" onClick={handleCreate} disabled={actionLoading === "create" || !newConnector.display_name}>
              {actionLoading === "create" ? <Spinner size="sm" /> : "Create Connector"}
            </Button>
          </div>
        </div>
      </Modal>
    </div>
  );
}
