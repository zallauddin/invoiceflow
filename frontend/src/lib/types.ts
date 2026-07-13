/**
 * InvoiceFlow TypeScript types.
 *
 * ⚠️  DO NOT EDIT MANUALLY — all types are sourced from the auto-generated
 * OpenAPI types in api-types.generated.ts.  Regenerate with:
 *
 *   npm run generate-types
 *
 * That command runs `openapi-typescript ./src/lib/openapi.json -o ./src/lib/api-types.generated.ts`.
 */

import type { components } from "./api-types.generated";

// ─── Enums ───────────────────────────────────────────────────────────────

export type InvoiceStatus = components["schemas"]["InvoiceStatus"];
export type ComplianceModel = components["schemas"]["ComplianceModel"];
export type ExtractionMethod = components["schemas"]["ExtractionMethod"];
export type IngestionSourceType = components["schemas"]["IngestionSource"];

// ─── Core entities ──────────────────────────────────────────────────────

export type Invoice = components["schemas"]["Invoice"];
export type InvoiceLine = components["schemas"]["InvoiceLine"];
export type InvoiceLineCreate = components["schemas"]["InvoiceLineCreate"];
export type InvoiceCreate = components["schemas"]["InvoiceCreate"];
export type InvoiceUpdate = components["schemas"]["InvoiceUpdate"];
export type InvoiceListResponse = components["schemas"]["InvoiceListResponse"];

// ─── Auth ────────────────────────────────────────────────────────────────

export type TokenRequest = components["schemas"]["TokenRequest"];
export type TokenResponse = components["schemas"]["TokenResponse"];
export type RegisterRequest = components["schemas"]["RegisterRequest"];
export type UserResponse = components["schemas"]["UserResponse"];

// ─── Ingestion ───────────────────────────────────────────────────────────

export type FileUploadResponse = components["schemas"]["FileUploadResponse"];
export type WebhookPayload = components["schemas"]["WebhookPayload"];
export type PollResponse = components["schemas"]["PollResponse"];
export type IngestionStatsResponse = components["schemas"]["IngestionStatsResponse"];
export type TaskStatusResponse = components["schemas"]["TaskStatusResponse"];

// ─── Analytics ───────────────────────────────────────────────────────────

export type DashboardStats = components["schemas"]["DashboardStats"];
export type ChartData = components["schemas"]["ChartData"];

// ─── Compliance ──────────────────────────────────────────────────────────

export type ComplianceStatusResponse = components["schemas"]["ComplianceStatusResponse"];
export type ComplianceValidationResponse = components["schemas"]["ComplianceValidationResponse"];
export type ComplianceConfigCreate = components["schemas"]["ComplianceConfigCreate"];
export type ComplianceConfigResponse = components["schemas"]["ComplianceConfigResponse"];
export type ComplianceTaskResponse = components["schemas"]["ComplianceTaskResponse"];
export type BatchComplianceResponse = components["schemas"]["BatchComplianceResponse"];
export type ExtractionStatusResponse = components["schemas"]["ExtractionStatusResponse"];

// ─── Settings ────────────────────────────────────────────────────────────

export type TenantSettings = components["schemas"]["TenantSettings"];
export type CountryConfig = components["schemas"]["CountryConfig"];
export type CountryConfigList = components["schemas"]["CountryConfigList"];

// ─── Connectors ──────────────────────────────────────────────────────────

export type AvailableConnector = components["schemas"]["AvailableConnector"];
export type ConnectorCreate = components["schemas"]["ConnectorCreate"];
export type ConnectorResponse = components["schemas"]["ConnectorResponse"];
export type ConnectorSyncRequest = components["schemas"]["ConnectorSyncRequest"];
export type ConnectorSyncResponse = components["schemas"]["ConnectorSyncResponse"];
export type ConnectorTestResponse = components["schemas"]["ConnectorTestResponse"];
export type ActivationResponse = components["schemas"]["ActivationResponse"];

// ─── Webhooks ────────────────────────────────────────────────────────────

export type WebhookCreate = components["schemas"]["WebhookCreate"];
export type WebhookUpdate = components["schemas"]["WebhookUpdate"];
export type WebhookResponse = components["schemas"]["WebhookResponse"];
export type WebhookTestResponse = components["schemas"]["WebhookTestResponse"];
export type ToggleResponse = components["schemas"]["ToggleResponse"];

// ─── Common ──────────────────────────────────────────────────────────────

export type MessageResponse = components["schemas"]["MessageResponse"];
export type ErrorResponse = components["schemas"]["ErrorResponse"];

// ─── Legacy aliases (backward-compatible names from old hand-maintained types) ─

/** @deprecated Use ComplianceConfigResponse instead */
export type ComplianceConfig = ComplianceConfigResponse;

/** @deprecated Use AuditLog from api-types directly */
export type AuditLog = components["schemas"]["AuditLog"];

/** @deprecated Use TenantSettings instead */
export type Tenant = TenantSettings;
