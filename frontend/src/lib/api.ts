/**
 * InvoiceFlow API client — fully typed from the auto-generated OpenAPI spec.
 *
 * Regenerate types with:  npm run generate-types
 * Then this file will type-check against the latest backend schemas.
 */

import type { paths, components } from "./api-types.generated";

const API_BASE = process.env.NEXT_PUBLIC_API_URL || "/api/v1";

/* ─── Type helpers — extract response bodies from OpenAPI operations ─── */

/** Extract the JSON response body from a response object. */
type OpBodyContent<T> = T extends { content: { "application/json": infer B } } ? B : never;

/** Success (2xx) response body for an operation.
 *  Uses `Code & keyof R` to filter the status code union to only valid keys,
 *  so endpoints that only return 200 (most GETs) or only 201 (creates) both work. */
type OpResp<
  P extends keyof paths,
  M extends keyof paths[P],
  Code extends number = 200 | 201
> = paths[P][M] extends { responses: infer R }
  ? OpBodyContent<R[Code & keyof R]>
  : never;

/** Request body for an operation. */
type OpBody<
  P extends keyof paths,
  M extends keyof paths[P]
> = paths[P][M] extends { requestBody: { content: { "application/json": infer B } } }
  ? B
  : never;

/* ─── Auth helpers ─────────────────────────────────────────── */

function getToken(): string | null {
  if (typeof window === "undefined") return null;
  return localStorage.getItem("invoiceflow_token");
}

export function setToken(token: string) {
  if (typeof window !== "undefined") localStorage.setItem("invoiceflow_token", token);
}

export function clearToken() {
  if (typeof window !== "undefined") localStorage.removeItem("invoiceflow_token");
}

export function isAuthenticated(): boolean {
  return !!getToken();
}

/* ─── Core request helper ─────────────────────────────────── */

async function request<T>(path: string, options?: RequestInit): Promise<T> {
  const url = `${API_BASE}${path}`;
  const token = getToken();
  const headers: Record<string, string> = {
    "Content-Type": "application/json",
    ...(options?.headers as Record<string, string>),
  };
  if (token) headers["Authorization"] = `Bearer ${token}`;

  const res = await fetch(url, { ...options, headers });

  if (res.status === 401) {
    clearToken();
    if (typeof window !== "undefined") window.location.href = "/login";
    throw new Error("Unauthorized");
  }

  if (!res.ok) {
    const error = await res.json().catch(() => ({ detail: res.statusText }));
    throw new Error(error.detail || `API error: ${res.status}`);
  }

  if (res.status === 204) return undefined as T;
  return res.json();
}

/* ─── Auth API ─────────────────────────────────────────────── */

type LoginResp = OpResp<"/api/v1/auth/login", "post">;
type RegisterReq = OpBody<"/api/v1/auth/register", "post">;
type RegisterResp = OpResp<"/api/v1/auth/register", "post", 201>;
type MeResp = OpResp<"/api/v1/auth/me", "get">;

export const authApi = {
  login: async (email: string, password: string) => {
    const data = await request<LoginResp>("/auth/login", {
      method: "POST",
      body: JSON.stringify({ email, password }),
    });
    setToken(data.access_token);
    return data;
  },

  register: (data: RegisterReq) =>
    request<RegisterResp>("/auth/register", {
      method: "POST",
      body: JSON.stringify(data),
    }),

  me: () => request<MeResp>("/auth/me"),
};

/* ─── Invoices API ─────────────────────────────────────────── */

type ListInvoicesResp = OpResp<"/api/v1/invoices", "get">;
type CreateInvoiceReq = OpBody<"/api/v1/invoices", "post">;
type CreateInvoiceResp = OpResp<"/api/v1/invoices", "post", 201>;
type GetInvoiceResp = OpResp<"/api/v1/invoices/{invoice_id}", "get">;
type UpdateInvoiceReq = OpBody<"/api/v1/invoices/{invoice_id}", "put">;
type UpdateInvoiceResp = OpResp<"/api/v1/invoices/{invoice_id}", "put">;

type InvoiceStatus = components["schemas"]["InvoiceStatus"];

export interface InvoiceListParams {
  page?: number;
  page_size?: number;
  status?: InvoiceStatus;
  country?: string;
  search?: string;
}

export const invoicesApi = {
  list: (params?: InvoiceListParams) => {
    const sp = new URLSearchParams();
    if (params?.page) sp.set("page", String(params.page));
    if (params?.page_size) sp.set("page_size", String(params.page_size));
    if (params?.status) sp.set("status", params.status);
    if (params?.country) sp.set("country", params.country);
    if (params?.search) sp.set("search", params.search);
    const qs = sp.toString();
    return request<ListInvoicesResp>(`/invoices${qs ? `?${qs}` : ""}`);
  },

  get: (id: string) => request<GetInvoiceResp>(`/invoices/${id}`),

  create: (data: CreateInvoiceReq) =>
    request<CreateInvoiceResp>("/invoices", {
      method: "POST",
      body: JSON.stringify(data),
    }),

  update: (id: string, data: UpdateInvoiceReq) =>
    request<UpdateInvoiceResp>(`/invoices/${id}`, {
      method: "PUT",
      body: JSON.stringify(data),
    }),

  approve: (id: string) =>
    request<GetInvoiceResp>(`/invoices/${id}/approve`, { method: "POST" }),

  reject: (id: string) =>
    request<GetInvoiceResp>(`/invoices/${id}/reject`, { method: "POST" }),

  delete: (id: string) =>
    request<void>(`/invoices/${id}`, { method: "DELETE" }),
};

/* ─── Ingestion API ────────────────────────────────────────── */

type UploadResp = OpResp<"/api/v1/ingestion/upload", "post">;
type WebhookReq = OpBody<"/api/v1/ingestion/webhook", "post">;
type PollResp = OpResp<"/api/v1/ingestion/poll/email", "post">;
type IngestionStats = OpResp<"/api/v1/ingestion/stats", "get">;
type IngestionTaskStatus = OpResp<"/api/v1/ingestion/task/{task_id}", "get">;

export const ingestionApi = {
  upload: async (file: File) => {
    const formData = new FormData();
    formData.append("file", file);
    const token = getToken();
    const headers: Record<string, string> = {};
    if (token) headers["Authorization"] = `Bearer ${token}`;
    const res = await fetch(`${API_BASE}/ingestion/upload`, {
      method: "POST",
      headers,
      body: formData,
    });
    if (!res.ok) {
      const error = await res.json().catch(() => ({ detail: res.statusText }));
      throw new Error(error.detail || `Upload error: ${res.status}`);
    }
    return res.json() as Promise<UploadResp>;
  },

  stats: () => request<IngestionStats>("/ingestion/stats"),

  pollEmail: () => request<PollResp>("/ingestion/poll/email", { method: "POST" }),

  pollFtp: () => request<PollResp>("/ingestion/poll/ftp", { method: "POST" }),

  pollSftp: () => request<PollResp>("/ingestion/poll/sftp", { method: "POST" }),

  taskStatus: (taskId: string) =>
    request<IngestionTaskStatus>(`/ingestion/task/${taskId}`),
};

/* ─── Analytics API ────────────────────────────────────────── */

type DashboardStats = OpResp<"/api/v1/analytics/dashboard", "get">;
type DailyVolume = OpResp<"/api/v1/analytics/charts/daily", "get">;
type ByCountry = OpResp<"/api/v1/analytics/charts/by-country", "get">;

export const analyticsApi = {
  dashboard: () => request<DashboardStats>("/analytics/dashboard"),
  daily: () => request<DailyVolume>("/analytics/charts/daily"),
  byCountry: () => request<ByCountry>("/analytics/charts/by-country"),
};

/* ─── Compliance API ───────────────────────────────────────── */

type ComplianceStatus = OpResp<"/api/v1/compliance/status", "get">;
type ComplianceValidation = OpResp<"/api/v1/compliance/validate/{invoice_id}", "post">;
type ComplianceTask = OpResp<"/api/v1/compliance/process/{invoice_id}", "post">;
type BatchCompliance = OpResp<"/api/v1/compliance/process-batch", "post">;
type ComplianceConfigs = OpResp<"/api/v1/compliance/config", "get">;
type CreateConfigReq = OpBody<"/api/v1/compliance/config", "post">;
type CreateConfigResp = OpResp<"/api/v1/compliance/config", "post">;
type TaskStatus = OpResp<"/api/v1/compliance/task/{task_id}", "get">;

export const complianceApi = {
  status: () => request<ComplianceStatus>("/compliance/status"),

  validate: (id: string) =>
    request<ComplianceValidation>(`/compliance/validate/${id}`, { method: "POST" }),

  process: (id: string) =>
    request<ComplianceTask>(`/compliance/process/${id}`, { method: "POST" }),

  processBatch: (invoiceIds: string[]) =>
    request<BatchCompliance>("/compliance/process-batch", {
      method: "POST",
      body: JSON.stringify({ invoice_ids: invoiceIds }),
    }),

  transmit: (id: string) =>
    request<ComplianceTask>(`/compliance/transmit/${id}`, { method: "POST" }),

  archive: (id: string) =>
    request<ComplianceTask>(`/compliance/archive/${id}`, { method: "POST" }),

  getConfigs: () => request<ComplianceConfigs>("/compliance/config"),

  createConfig: (data: CreateConfigReq) =>
    request<CreateConfigResp>("/compliance/config", {
      method: "POST",
      body: JSON.stringify(data),
    }),

  taskStatus: (taskId: string) =>
    request<TaskStatus>(`/compliance/task/${taskId}`),
};

/* ─── Extraction API ───────────────────────────────────────── */

type ExtractionMsg = OpResp<"/api/v1/extraction/extract/{invoice_id}", "post">;
type ExtractionStatus = OpResp<"/api/v1/extraction/status/{invoice_id}", "get">;

export const extractionApi = {
  extract: (invoiceId: string) =>
    request<ExtractionMsg>(`/extraction/extract/${invoiceId}`, { method: "POST" }),

  extractBatch: (invoiceIds: string[]) =>
    request<ExtractionMsg>("/extraction/extract-batch", {
      method: "POST",
      body: JSON.stringify({ invoice_ids: invoiceIds }),
    }),

  status: (invoiceId: string) =>
    request<ExtractionStatus>(`/extraction/status/${invoiceId}`),
};

/* ─── Settings API ─────────────────────────────────────────── */

type TenantSettings = OpResp<"/api/v1/settings/", "get">;
type UpdateSettingsResp = OpResp<"/api/v1/settings/", "put">;
type CountryConfigs = OpResp<"/api/v1/settings/countries", "get">;

export const settingsApi = {
  get: () => request<TenantSettings>("/settings/"),

  update: (data: Record<string, unknown>) =>
    request<UpdateSettingsResp>("/settings/", {
      method: "PUT",
      body: JSON.stringify(data),
    }),

  getCountries: () => request<CountryConfigs>("/settings/countries"),
};

/* ─── ERP Connectors API ───────────────────────────────────── */

type AvailableConnectors = OpResp<"/api/v1/connectors/available", "get">;
type Connectors = OpResp<"/api/v1/connectors/", "get">;
type CreateConnectorReq = OpBody<"/api/v1/connectors/", "post">;
type CreateConnectorResp = OpResp<"/api/v1/connectors/", "post", 201>;
type ConnectorDetail = OpResp<"/api/v1/connectors/{connector_id}", "get">;
type ConnectorTest = OpResp<"/api/v1/connectors/{connector_id}/test", "post">;
type ConnectorSyncReq = OpBody<"/api/v1/connectors/{connector_id}/sync", "post">;
type ConnectorSync = OpResp<"/api/v1/connectors/{connector_id}/sync", "post">;
type Activation = OpResp<"/api/v1/connectors/{connector_id}/activate", "post">;

export const connectorsApi = {
  listAvailable: () => request<AvailableConnectors>("/connectors/available"),

  list: () => request<Connectors>("/connectors/"),

  get: (id: string) => request<ConnectorDetail>(`/connectors/${id}`),

  create: (data: CreateConnectorReq) =>
    request<CreateConnectorResp>("/connectors/", {
      method: "POST",
      body: JSON.stringify(data),
    }),

  delete: (id: string) =>
    request<void>(`/connectors/${id}`, { method: "DELETE" }),

  test: (id: string) =>
    request<ConnectorTest>(`/connectors/${id}/test`, { method: "POST" }),

  sync: (id: string, data: ConnectorSyncReq) =>
    request<ConnectorSync>(`/connectors/${id}/sync`, {
      method: "POST",
      body: JSON.stringify(data),
    }),

  activate: (id: string) =>
    request<Activation>(`/connectors/${id}/activate`, { method: "POST" }),

  deactivate: (id: string) =>
    request<Activation>(`/connectors/${id}/deactivate`, { method: "POST" }),
};

/* ─── Webhooks API ─────────────────────────────────────────── */

type WebhookEvents = OpResp<"/api/v1/webhooks/events", "get">;
type Webhooks = OpResp<"/api/v1/webhooks/", "get">;
type CreateWebhookReq = OpBody<"/api/v1/webhooks/", "post">;
type CreateWebhookResp = OpResp<"/api/v1/webhooks/", "post", 201>;
type WebhookDetail = OpResp<"/api/v1/webhooks/{webhook_id}", "get">;
type UpdateWebhookReq = OpBody<"/api/v1/webhooks/{webhook_id}", "put">;
type UpdateWebhookResp = OpResp<"/api/v1/webhooks/{webhook_id}", "put">;
type WebhookTest = OpResp<"/api/v1/webhooks/{webhook_id}/test", "post">;
type WebhookToggle = OpResp<"/api/v1/webhooks/{webhook_id}/toggle", "post">;

export const webhooksApi = {
  listEvents: () => request<WebhookEvents>("/webhooks/events"),

  list: () => request<Webhooks>("/webhooks/"),

  get: (id: string) => request<WebhookDetail>(`/webhooks/${id}`),

  create: (data: CreateWebhookReq) =>
    request<CreateWebhookResp>("/webhooks/", {
      method: "POST",
      body: JSON.stringify(data),
    }),

  update: (id: string, data: UpdateWebhookReq) =>
    request<UpdateWebhookResp>(`/webhooks/${id}`, {
      method: "PUT",
      body: JSON.stringify(data),
    }),

  delete: (id: string) =>
    request<void>(`/webhooks/${id}`, { method: "DELETE" }),

  test: (id: string) =>
    request<WebhookTest>(`/webhooks/${id}/test`, { method: "POST" }),

  toggle: (id: string) =>
    request<WebhookToggle>(`/webhooks/${id}/toggle`, { method: "POST" }),
};
