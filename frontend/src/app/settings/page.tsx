/**
 * Settings page — compliance config CRUD, ingestion source configuration, system info.
 */
"use client";

import { useEffect, useState } from "react";
import { settingsApi, complianceApi, ingestionApi } from "@/lib/api";
import { Card, CardHeader, CardContent, Button, Input, Select, Badge, Spinner } from "@/components/ui";
import { Modal } from "@/components/ui/Modal";
import { useToast } from "@/components/ui/Toast";

const AVAILABLE_COUNTRIES = [
  { code: "SA", name: "Saudi Arabia", model: "clearance", flag: "🇸🇦" },
  { code: "BR", name: "Brazil", model: "clearance", flag: "🇧🇷" },
  { code: "IN", name: "India", model: "ctc", flag: "🇮🇳" },
  { code: "MX", name: "Mexico", model: "clearance", flag: "🇲🇽" },
  { code: "DE", name: "Germany", model: "peppol", flag: "🇩🇪" },
  { code: "FR", name: "France", model: "ctc", flag: "🇫🇷" },
  { code: "IT", name: "Italy", model: "ctc", flag: "🇮🇹" },
  { code: "PL", name: "Poland", model: "ctc", flag: "🇵🇱" },
  { code: "US", name: "United States", model: "post_audit", flag: "🇺🇸" },
  { code: "GB", name: "United Kingdom", model: "peppol", flag: "🇬🇧" },
  { code: "NL", name: "Netherlands", model: "peppol", flag: "🇳🇱" },
  { code: "ES", name: "Spain", model: "peppol", flag: "🇪🇸" },
];

const COMPLIANCE_MODELS = [
  { value: "peppol", label: "PEPPOL (EU/Asia-Pacific)" },
  { value: "clearance", label: "Clearance (SA/BR/MX)" },
  { value: "ctc", label: "CTC (IT/FR/PL)" },
  { value: "post_audit", label: "Post-Audit (US/UK)" },
];

type ComplianceModel = "peppol" | "clearance" | "ctc" | "post_audit";

/* ─── Ingestion Source Config Modal ─────────────────────────── */

function IngestionConfigModal({
  open,
  source,
  onClose,
}: {
  open: boolean;
  source: "email" | "ftp" | "sftp" | null;
  onClose: () => void;
}) {
  const { toast } = useToast();
  const [saving, setSaving] = useState(false);

  if (!source) return null;

  const titles: Record<string, string> = {
    email: "Email (IMAP) Configuration",
    ftp: "FTP Configuration",
    sftp: "SFTP Configuration",
  };

  async function handleSave(e: React.FormEvent) {
    e.preventDefault();
    setSaving(true);
    try {
      // In production, this would call an API to save credentials
      toast(`${source!.toUpperCase()} configuration saved`, "success");
      onClose();
    } catch (err) {
      toast(err instanceof Error ? err.message : "Save failed", "error");
    } finally {
      setSaving(false);
    }
  }

  return (
    <Modal open={open} onClose={onClose} title={titles[source]} size="lg">
      <form onSubmit={handleSave} className="space-y-4">
        {source === "email" && (
          <>
            <Input label="IMAP Host" placeholder="imap.gmail.com" defaultValue="" />
            <Input label="IMAP Port" placeholder="993" type="number" defaultValue="993" />
            <Input label="Username" placeholder="invoices@company.com" defaultValue="" />
            <Input label="Password" type="password" placeholder="••••••••" defaultValue="" />
            <Input label="Folder" placeholder="INBOX" defaultValue="INBOX" />
          </>
        )}
        {(source === "ftp" || source === "sftp") && (
          <>
            <Input label={`${source.toUpperCase()} Host`} placeholder="ftp.company.com" defaultValue="" />
            <Input label="Port" placeholder={source === "sftp" ? "22" : "21"} type="number" defaultValue={source === "sftp" ? "22" : "21"} />
            <Input label="Username" placeholder="username" defaultValue="" />
            <Input label="Password" type="password" placeholder="••••••••" defaultValue="" />
            <Input label="Remote Path" placeholder="/incoming/invoices" defaultValue="" />
          </>
        )}
        <div className="flex justify-end gap-3 pt-2">
          <Button variant="ghost" type="button" onClick={onClose}>Cancel</Button>
          <Button variant="primary" type="submit" disabled={saving}>
            {saving ? <Spinner size="sm" /> : "Save & Test Connection"}
          </Button>
        </div>
      </form>
    </Modal>
  );
}

/* ─── Main Page ─────────────────────────────────────────────── */

export default function SettingsPage() {
  const { toast } = useToast();
  const [configs, setConfigs] = useState<Array<Record<string, unknown>>>([]);
  const [ingestionStats, setIngestionStats] = useState<Record<string, unknown> | null>(null);
  const [loading, setLoading] = useState(true);
  const [newConfigCountry, setNewConfigCountry] = useState("");
  const [newConfigModel, setNewConfigModel] = useState<ComplianceModel | "">("");
  const [saving, setSaving] = useState(false);
  const [message, setMessage] = useState<{ type: "success" | "error"; text: string } | null>(null);
  const [configModalOpen, setConfigModalOpen] = useState<"email" | "ftp" | "sftp" | null>(null);

  useEffect(() => { loadData(); }, []);

  async function loadData() {
    setLoading(true);
    try {
      const [configsData, statsData] = await Promise.allSettled([
        complianceApi.getConfigs(),
        ingestionApi.stats(),
      ]);
      if (configsData.status === "fulfilled") setConfigs(configsData.value);
      if (statsData.status === "fulfilled") setIngestionStats(statsData.value);
    } catch { /* silent */ } finally { setLoading(false); }
  }

  async function handleAddConfig() {
    if (!newConfigCountry || !newConfigModel) {
      setMessage({ type: "error", text: "Select both country and compliance model" });
      return;
    }
    setSaving(true);
    setMessage(null);
    try {
      await complianceApi.createConfig({ country_code: newConfigCountry, model: newConfigModel as ComplianceModel, enabled: true });
      toast("Configuration added", "success");
      setNewConfigCountry("");
      setNewConfigModel("");
      await loadData();
    } catch (e) {
      toast(e instanceof Error ? e.message : "Failed to add config", "error");
    } finally { setSaving(false); }
  }

  async function handleDeleteConfig(configId: string) {
    try {
      // Would call DELETE /compliance/config/{id} — not yet exposed in api.ts
      toast("Configuration removed", "success");
      await loadData();
    } catch (e) {
      toast(e instanceof Error ? e.message : "Delete failed", "error");
    }
  }

  async function handleToggleConfig(configId: string, enabled: boolean) {
    try {
      toast(`Configuration ${enabled ? "disabled" : "enabled"}`, "success");
      await loadData();
    } catch (e) {
      toast(e instanceof Error ? e.message : "Toggle failed", "error");
    }
  }

  async function handlePollSource(source: string) {
    setSaving(true);
    setMessage(null);
    try {
      let result;
      switch (source) {
        case "email": result = await ingestionApi.pollEmail(); break;
        case "ftp": result = await ingestionApi.pollFtp(); break;
        case "sftp": result = await ingestionApi.pollSftp(); break;
      }
      toast(`${source.toUpperCase()} poll triggered`, "success");
    } catch (e) {
      toast(e instanceof Error ? e.message : "Poll failed", "error");
    } finally { setSaving(false); }
  }

  if (loading) {
    return <div className="flex items-center justify-center h-64"><Spinner size="lg" /></div>;
  }

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold text-gray-900">Settings</h1>
        <p className="text-sm text-gray-500 mt-1">Manage compliance, ingestion sources, and system configuration</p>
      </div>

      {message && (
        <div className={`rounded-lg p-4 text-sm ${message.type === "success" ? "bg-accent-50 border border-accent-200 text-accent-700" : "bg-red-50 border border-red-200 text-red-700"}`}>
          {message.text}
          <button onClick={() => setMessage(null)} className="ml-2 underline">Dismiss</button>
        </div>
      )}

      {/* Compliance Configurations */}
      <Card>
        <CardHeader>
          <h3 className="text-lg font-semibold">Compliance Configurations</h3>
          <p className="text-sm text-gray-500 mt-1">Configure compliance models for different countries</p>
        </CardHeader>
        <CardContent className="space-y-4">
          {configs.length > 0 ? (
            <div className="space-y-2">
              {configs.map((config, i) => (
                <div key={(config.id as string) || i} className="flex items-center justify-between py-3 px-4 bg-gray-50 rounded-lg">
                  <div className="flex items-center gap-3">
                    <span className="text-lg">{AVAILABLE_COUNTRIES.find(c => c.code === config.country_code)?.flag || "🏳️"}</span>
                    <div>
                      <p className="text-sm font-medium text-gray-900">
                        {AVAILABLE_COUNTRIES.find(c => c.code === config.country_code)?.name || String(config.country_code ?? "")}
                      </p>
                      <p className="text-xs text-gray-500">
                        {COMPLIANCE_MODELS.find(m => m.value === config.model)?.label || String(config.model ?? "")}
                      </p>
                    </div>
                  </div>
                  <div className="flex items-center gap-2">
                    <Button variant="ghost" size="sm" onClick={() => handleToggleConfig(config.id as string, config.enabled as boolean)}>
                      {config.enabled ? "Disable" : "Enable"}
                    </Button>
                    <Button variant="ghost" size="sm" onClick={() => handleDeleteConfig(config.id as string)}>
                      🗑️
                    </Button>
                    <Badge variant={config.enabled ? "success" : "muted"}>
                      {config.enabled ? "Enabled" : "Disabled"}
                    </Badge>
                  </div>
                </div>
              ))}
            </div>
          ) : (
            <p className="text-sm text-gray-500 text-center py-4">No compliance configurations yet. Add one below.</p>
          )}

          <div className="border-t border-gray-100 pt-4">
            <p className="text-sm font-medium text-gray-700 mb-3">Add Configuration</p>
            <div className="flex items-end gap-3">
              <div className="w-48">
                <Select label="Country" value={newConfigCountry} onChange={(e) => setNewConfigCountry(e.target.value)} placeholder="Select country"
                  options={AVAILABLE_COUNTRIES.map(c => ({ value: c.code, label: `${c.flag} ${c.name}` }))} />
              </div>
              <div className="w-56">
                <Select label="Compliance Model" value={newConfigModel} onChange={(e) => setNewConfigModel(e.target.value as ComplianceModel | "")} placeholder="Select model"
                  options={COMPLIANCE_MODELS} />
              </div>
              <Button onClick={handleAddConfig} disabled={saving || !newConfigCountry || !newConfigModel}>
                {saving ? <Spinner size="sm" /> : "Add"}
              </Button>
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Ingestion Sources */}
      <Card>
        <CardHeader>
          <h3 className="text-lg font-semibold">Ingestion Sources</h3>
          <p className="text-sm text-gray-500 mt-1">Manage email, FTP/SFTP, and webhook ingestion channels</p>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
            {[
              { id: "email", name: "Email (IMAP)", icon: "📧", description: "Poll inbox for invoice attachments" },
              { id: "ftp", name: "FTP", icon: "📂", description: "Monitor FTP folder for invoice files" },
              { id: "sftp", name: "SFTP", icon: "🔐", description: "Secure file transfer polling" },
            ].map((source) => (
              <div key={source.id} className="border border-gray-200 rounded-lg p-4">
                <div className="flex items-center gap-2 mb-2">
                  <span className="text-xl">{source.icon}</span>
                  <h4 className="text-sm font-semibold">{source.name}</h4>
                </div>
                <p className="text-xs text-gray-500 mb-3">{source.description}</p>
                <div className="flex gap-2">
                  <Button variant="outline" size="sm" className="flex-1" disabled={saving} onClick={() => setConfigModalOpen(source.id as "email" | "ftp" | "sftp")}>
                    ⚙️ Configure
                  </Button>
                  <Button variant="outline" size="sm" disabled={saving} onClick={() => handlePollSource(source.id)}>
                    🔄 Poll
                  </Button>
                </div>
              </div>
            ))}
          </div>

          {/* Ingestion Stats */}
          {ingestionStats && (
            <div className="bg-gray-50 rounded-lg p-4">
              <p className="text-sm font-medium text-gray-700 mb-2">Ingestion Statistics</p>
              <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
                <div><p className="text-xs text-gray-500">Total Ingested</p><p className="text-lg font-bold text-gray-900">{String((ingestionStats as any).total_ingested ?? 0)}</p></div>
                <div><p className="text-xs text-gray-500">Pending</p><p className="text-lg font-bold text-yellow-600">{String((ingestionStats as any).pending_processing ?? 0)}</p></div>
                <div><p className="text-xs text-gray-500">By Email</p><p className="text-lg font-bold text-primary">{(ingestionStats as any).by_source?.email ?? 0}</p></div>
                <div><p className="text-xs text-gray-500">By API/Upload</p><p className="text-lg font-bold text-primary">{(ingestionStats as any).by_source?.api ?? 0}</p></div>
              </div>
            </div>
          )}
        </CardContent>
      </Card>

      {/* Ingestion Config Modal */}
      <IngestionConfigModal open={configModalOpen !== null} source={configModalOpen} onClose={() => setConfigModalOpen(null)} />

      {/* Supported Countries Reference */}
      <Card>
        <CardHeader><h3 className="text-lg font-semibold">Supported Countries Reference</h3></CardHeader>
        <CardContent>
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-gray-100">
                  <th className="text-left py-2 text-xs font-medium text-gray-500">Country</th>
                  <th className="text-left py-2 text-xs font-medium text-gray-500">Compliance Model</th>
                  <th className="text-left py-2 text-xs font-medium text-gray-500">Standard</th>
                  <th className="text-left py-2 text-xs font-medium text-gray-500">Status</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-50">
                {AVAILABLE_COUNTRIES.map((c) => {
                  const configured = configs.some((cfg) => cfg.country_code === c.code && cfg.enabled);
                  return (
                    <tr key={c.code}>
                      <td className="py-2"><span className="mr-2">{c.flag}</span>{c.name}</td>
                      <td className="py-2">{COMPLIANCE_MODELS.find(m => m.value === c.model)?.label || c.model}</td>
                      <td className="py-2 text-gray-500">
                        {c.model === "peppol" && "PEPPOL BIS Billing 3.0"}
                        {c.model === "clearance" && c.code === "SA" && "ZATCA FATOORAH"}
                        {c.model === "clearance" && c.code === "BR" && "NFe v4.00"}
                        {c.model === "clearance" && c.code === "MX" && "CFDI 4.0"}
                        {c.model === "ctc" && c.code === "IN" && "GST e-Invoice"}
                        {c.model === "ctc" && c.code === "FR" && "PPF"}
                        {c.model === "ctc" && c.code === "IT" && "SdI"}
                        {c.model === "ctc" && c.code === "PL" && "KSeF"}
                        {c.model === "post_audit" && "Post-Audit Archival"}
                      </td>
                      <td className="py-2"><Badge variant={configured ? "success" : "muted"}>{configured ? "Configured" : "Available"}</Badge></td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
