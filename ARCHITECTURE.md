# InvoiceFlow — Architecture & Design

> Version 1.0 | July 2026
> AI-powered e-invoice processing platform with global compliance

---

## Table of Contents

1. [System Overview](#1-system-overview)
2. [Architecture Diagram](#2-architecture-diagram)
3. [Technology Stack](#3-technology-stack)
4. [Data Flow](#4-data-flow)
5. [Domain Model](#5-domain-model)
6. [API Layer](#6-api-layer)
7. [Feature Coverage](#7-feature-coverage)
8. [Security & Compliance](#8-security--compliance)
9. [Deployment Architecture](#9-deployment-architecture)

---

## 1. System Overview

InvoiceFlow is a multi-tenant, AI-powered e-invoice processing platform that supports **30+ countries** with global compliance standards including PEPPOL, ZATCA, Brazil NF-e, India IRP, Mexico CFDI, Italy SdI, France PPF, and Poland KSeF.

### Core Capabilities

| Capability | Description |
|------------|-------------|
| **Multi-source Ingestion** | Email (IMAP), FTP/SFTP, REST API, file upload, webhook |
| **AI Extraction** | Hybrid OCR (Tesseract) + LLM fallback (Claude/GPT) |
| **Format Support** | PDF, images, XML (UBL, CII, Factur-X/ZUGFeRD) |
| **Compliance Engine** | 8 compliance models with unified orchestrator |
| **Document Management** | Versioning, relationships, full-text search |
| **ERP Integration** | Xero, SAP, Oracle connectors with sync |
| **Real-time Dashboard** | SignalR WebSocket push replacing polling |
| **Multi-tenant Isolation** | Row-level security via global query filters |

---

## 2. Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────────────┐
│                         CLIENT LAYER                                    │
│  ┌─────────────────────────────────────────────────────────────────┐   │
│  │  Next.js 14 Frontend (React 18, TypeScript, Tailwind CSS)       │   │
│  │  ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌───────────────────┐  │   │
│  │  │Dashboard │ │Invoices  │ │Connectors│ │Settings/Webhooks   │  │   │
│  │  │+ SignalR │ │+ Bulk    │ │+ Sync    │ │+ Compliance Config │  │   │
│  │  └──────────┘ └──────────┘ └──────────┘ └───────────────────┘  │   │
│  └─────────────────────────────────────────────────────────────────┘   │
└──────────────────────────┬──────────────────────────────────────────────┘
                           │
                    ┌──────┴──────┐
                    │   Nginx     │  API Gateway (port 80/443)
                    │  Reverse    │
                    │   Proxy     │
                    └──────┬──────┘
                           │
┌──────────────────────────┴──────────────────────────────────────────────┐
│                       API LAYER (Dual Backend)                         │
│                                                                        │
│  ┌──────────────────────────────┐    ┌──────────────────────────────┐  │
│  │  .NET 10 API (Port 5231)    │    │  Python FastAPI (Port 8000)  │  │
│  │  ─────────────────────────  │    │  ──────────────────────────  │  │
│  │  • Authentication (JWT)     │    │  • AI Extraction (LLM)       │  │
│  │  • File Ingestion           │    │  • Compliance Engine         │  │
│  │  • Invoice CRUD             │    │  • ERP Connectors            │  │
│  │  • Document Management      │    │  • Webhook Dispatcher        │  │
│  │  • Approval Workflows       │    │  • Analytics API             │  │
│  │  • CSV/Excel Export         │    │  • OpenAPI Schema            │  │
│  │  • SignalR Hub              │    │                              │  │
│  │  • API Key Auth             │    │                              │  │
│  │  • Health / Metrics         │    │                              │  │
│  └──────────────┬───────────────┘    └──────────────┬───────────────┘  │
└─────────────────┼───────────────────────────────────┼──────────────────┘
                  │                                   │
┌─────────────────┴───────────────────────────────────┴──────────────────┐
│                     BACKGROUND WORKER LAYER                            │
│                                                                        │
│  ┌─────────────────────────────────────────────────────────────┐      │
│  │  .NET Worker (MassTransit + Quartz.NET)                     │      │
│  │  ┌──────────────────────┐  ┌──────────────────────────┐     │      │
│  │  │ 20+ Event Consumers  │  │  Scheduled Ingestion     │     │      │
│  │  │ • InvoiceReceived    │  │  • Email Poll (IMAP)     │     │      │
│  │  │ • InvoiceExtracted   │  │  • FTP/SFTP Poll         │     │      │
│  │  │ • InvoiceApproved    │  │  • Escalation Processing │     │      │
│  │  │ • Credit/Debit Notes │  │                          │     │      │
│  │  │ • Purchase Orders    │  │                          │     │      │
│  │  │ • Delivery Notes     │  │                          │     │      │
│  │  │ • Payment Reminders  │  │                          │     │      │
│  │  └──────────────────────┘  └──────────────────────────┘     │      │
│  └─────────────────────────────────────────────────────────────┘      │
│                                                                        │
│  ┌─────────────────────────────────────────────────────────────┐      │
│  │  Python Celery Workers (4 queues)                          │      │
│  │  ┌─────────────┐ ┌──────────┐ ┌──────────┐ ┌───────────┐  │      │
│  │  │  Ingestion  │ │Extraction│ │Compliance│ │ Webhooks  │  │      │
│  │  └─────────────┘ └──────────┘ └──────────┘ └───────────┘  │      │
│  └─────────────────────────────────────────────────────────────┘      │
└────────────────────────────────────────────────────────────────────────┘
                                    │
┌───────────────────────────────────┴───────────────────────────────────┐
│                       DATA LAYER                                      │
│                                                                       │
│  ┌────────────┐  ┌──────────┐  ┌───────────┐  ┌───────────────┐     │
│  │ PostgreSQL │  │  Redis 7 │  │   MinIO   │  │   RabbitMQ    │     │
│  │    16      │  │  Cache + │  │  S3-Storage│  │   Message     │     │
│  │  + Tenant  │  │  SignalR │  │ Documents  │  │    Broker     │     │
│  │  Isolation │  │  Backend │  │ Thumbnails │  │   + Retry     │     │
│  └────────────┘  └──────────┘  └───────────┘  └───────────────┘     │
│                                                                       │
│  ┌──────────────────────────────────────────────────────────────┐    │
│  │  Monitoring Stack: Prometheus + Grafana + Serilog + Seq      │    │
│  └──────────────────────────────────────────────────────────────┘    │
└──────────────────────────────────────────────────────────────────────┘
```

---

## 3. Technology Stack

### Backend (.NET 10)

| Component | Technology | Purpose |
|-----------|-----------|---------|
| **Runtime** | .NET 10 (C# 14) | Primary API + Worker |
| **Pattern** | Clean Architecture | Core → Application → Infrastructure → API |
| **ORM** | EF Core 10 + Npgsql | PostgreSQL ORM with migrations |
| **Auth** | JWT Bearer + BCrypt | Token-based authentication |
| **Messaging** | MassTransit + RabbitMQ | Async event-driven processing |
| **Scheduling** | Quartz.NET | Cron-based job scheduling |
| **Caching** | Redis (StackExchange) | Distributed caching |
| **Storage** | MinIO Client | S3-compatible object storage |
| **Real-time** | SignalR + Redis Backplane | WebSocket push |
| **CQRS** | MediatR | Command/Query segregation |
| **Mapping** | Mapster | Object-to-object mapping |
| **Validation** | FluentValidation | Input validation |
| **Monitoring** | Prometheus + Serilog | Metrics + structured logging |

### Backend (Python 3.12)

| Component | Technology | Purpose |
|-----------|-----------|---------|
| **Framework** | FastAPI + Uvicorn | Async REST API |
| **ORM** | SQLAlchemy 2.0 (async) | Database access |
| **AI/LLM** | Anthropic Claude + OpenAI GPT | Intelligent extraction |
| **OCR** | Tesseract (pytesseract) | Document OCR |
| **Task Queue** | Celery + Redis | Background processing |
| **Migration** | Alembic | Schema migration |
| **Email** | python-jose + passlib | Auth utilities |

### Frontend (Next.js 14)

| Component | Technology | Purpose |
|-----------|-----------|---------|
| **Framework** | Next.js 14 (App Router) | Full-stack React |
| **Language** | TypeScript 5.7 | Type safety |
| **Styling** | Tailwind CSS 3.4 | Utility-first CSS |
| **Charts** | Recharts | Data visualization |
| **Icons** | Lucide React | Icon library |
| **i18n** | Custom provider | 8 languages + RTL |
| **Real-time** | @microsoft/signalr (pending) | WebSocket push |

### Infrastructure

| Component | Technology | Purpose |
|-----------|-----------|---------|
| **Container** | Docker + Docker Compose | Dev/Prod orchestration |
| **Database** | PostgreSQL 16 | Primary datastore |
| **Cache** | Redis 7 | Cache + message broker |
| **Storage** | MinIO (S3-compatible) | Document storage |
| **Broker** | RabbitMQ 3.13 | Message queue |
| **Monitoring** | Prometheus + Grafana | Metrics + dashboards |
| **Logging** | Serilog + Seq (optional) | Structured logging |

---

## 4. Data Flow

### Invoice Processing Pipeline

```
                    ┌───────────────┐
                    │  Ingestion    │
                    │  Email/FTP/   │
                    │  API/Webhook  │
                    └───────┬───────┘
                            │
                    ┌───────▼───────┐
                    │  Document     │
                    │  Stored in    │
                    │  MinIO        │
                    └───────┬───────┘
                            │
                    ┌───────▼───────┐
                    │  Extraction   │
                    │  OCR → LLM → │
                    │  Template     │
                    └───────┬───────┘
                            │
                    ┌───────▼───────┐
                    │  Human Review │
                    │  (if < 95%    │
                    │   confidence) │
                    └───────┬───────┘
                            │
                    ┌───────▼───────┐
                    │  Approval     │
                    │  Workflow     │
                    │  (0-N steps)  │
                    └───────┬───────┘
                            │
                    ┌───────▼───────┐
                    │  Compliance   │
                    │  PEPPOL/ZATCA/│
                    │  Brazil/etc.  │
                    └───────┬───────┘
                            │
                    ┌───────▼───────┐
                    │  Transmission │
                    │  + Archival   │
                    │  + Notification│
                    └───────────────┘
```

### Event Flow (MassTransit)

```
         ┌──────────┐     ┌──────────┐     ┌──────────┐
         │ Invoice  │────▶│Extraction│────▶│ Invoice  │
         │ Received │     │Command   │     │Extracted │
         └──────────┘     └──────────┘     └─────┬────┘
                                                 │
                    ┌────────────────────────────┼──────────────┐
                    │                            │              │
            ┌───────▼───────┐          ┌─────────▼────────┐    │
            │ Invoice       │          │ Invoice          │    │
            │ Approved      │          │ Failed           │    │
            └───────┬───────┘          └──────────────────┘    │
                    │                                          │
            ┌───────▼───────┐                                  │
            │ Compliance    │                                  │
            │ Processed     │                                  │
            └───────┬───────┘                                  │
                    │                                          │
            ┌───────▼───────┐                                  │
            │ Invoice       │                                  │
            │ Transmitted   │                                  │
            └───────────────┘                                  │
                                                               │
         Other Events: CreditNoteCreated, DebitNoteUpdated,    │
         PurchaseOrderConfirmed, DeliveryNoteDelivered,         │
         ReminderSent, ReminderEscalated                        │
         ──────────────────────────────────────────────────────┘
```

---

## 5. Domain Model

### Entity Relationships

```
┌─────────────────────────────────────────────────────────────────────────┐
│                          DOMAIN MODEL                                    │
│                                                                         │
│  ┌──────────┐    ┌──────────┐    ┌──────────────┐    ┌────────────┐   │
│  │  Tenant  │1──N│  User    │    │   Invoice    │1──N│ InvoiceLine│   │
│  └──────────┘    └──────────┘    └──────┬───────┘    └────────────┘   │
│        │                                │                              │
│        │1                               │1                             │
│        │                                │                              │
│        ├────────────────────────────────┤                              │
│        │                                │                              │
│  ┌─────┴─────────┐    ┌────────────────┼──────────────────────┐       │
│  │ Compliance    │    │                │                      │       │
│  │ Config        │    │         ┌──────▼──────┐       ┌───────▼────┐  │
│  └───────────────┘    │         │ Approval    │       │ Document   │  │
│                       │         │ Request     │       │ (DMS)      │  │
│  ┌───────────────┐    │         └─────────────┘       └───────┬────┘  │
│  │ Connector     │    │                                      │       │
│  │ Config        │    │    ┌─────────────────────────────────┼───────┤
│  └───────────────┘    │    │   ApprovalChain                 │       │
│                       │    │   ├─ ApprovalStep (1..N)        │       │
│  ┌───────────────┐    │    └─────────────────────────────────┘       │
│  │ Webhook       │    │                                              │
│  │ Config        │    │    ┌──────────────┐   ┌──────────────────┐   │
│  └───────────────┘    │    │ Document     │   │ DocumentVersion  │   │
│                       │    │ Relationship │   │ History          │   │
│  ┌───────────────┐    │    └──────────────┘   └──────────────────┘   │
│  │ ApiKey        │    │                                              │
│  └───────────────┘    │    ┌──────────────────────────────────────┐  │
│                       │    │ DocumentEntity (TPC Inheritance)     │  │
│  ┌───────────────┐    │    │ ├─ CreditNote                       │  │
│  │ AuditLog      │    │    │ ├─ DebitNote                        │  │
│  └───────────────┘    │    │ ├─ PurchaseOrder                    │  │
│                       │    │ ├─ DeliveryNote                      │  │
│                       │    │ └─ Reminder                         │  │
│                       │    └──────────────────────────────────────┘  │
└───────────────────────────────────────────────────────────────────────┘
```

### Entity Details

| Entity | Table | Key Fields |
|--------|-------|------------|
| **Tenant** | `tenants` | Name, Slug, TaxId, Country, IsActive |
| **User** | `users` | Email, DisplayName, PasswordHash, Role (Admin/User/Viewer) |
| **Invoice** | `invoices` | InvoiceNumber, Status, Vendor/Buyer, TotalAmount, CountryCode |
| **InvoiceLine** | `invoice_lines` | Description, Quantity, UnitPrice, TaxRate, LineTotal |
| **Document** | `documents` | FileName, MimeType, StoragePath, OcrText, SearchVector (tsvector) |
| **ApprovalRequest** | `approval_requests` | InvoiceId, Status (Pending/Approved/Rejected/Escalated), AssignedTo |
| **ApprovalChain** | `approval_chains` | Name, TargetEntityType, CountryCode, Min/Max TotalAmount |
| **ApprovalStep** | `approval_steps` | StepOrder, RequiredRole, DeadlineHours, EscalationAction |
| **ComplianceConfig** | `compliance_configs` | CountryCode, Model (Peppol/Zatca/Brazil/etc.), ConfigJson |
| **ConnectorConfig** | `erp_connector_configs` | ConnectorType (Xero/SAP/Oracle), Status, SyncDirection |
| **WebhookConfig** | `webhook_configs` | URL, Secret (HMAC), Events, MaxRetries, Timeout |
| **ApiKey** | `api_keys` | Key, Name, Permissions, ExpiresAt, LastUsedAt |
| **AuditLog** | `audit_logs` | Action, EntityType, EntityId, OldValues, NewValues |
| **RefreshToken** | `refresh_tokens` | Token, UserId, ExpiresAt, RevokedAt |

### Enum Types

| Enum | Values |
|------|--------|
| **UserRole** | Admin, User, Viewer |
| **InvoiceStatus** | Draft → Received → Extracting → Extracted → PendingApproval → Approved → Rejected → Processing → Compliant → NonCompliant → Transmitted → Failed → Cancelled |
| **ComplianceModel** | Peppol, Zatca, BrazilNfe, IndiaIrp, MexicoCfdi, ItalySdi, FrancePpf, PolandKsef, PostAudit |
| **DocumentType** | Invoice, CreditNote, DebitNote, PurchaseOrder, DeliveryNote, Reminder |
| **ApprovalStatus** | Pending, Approved, Rejected, Escalated |
| **ConnectorType** | Xero, SAP, Oracle |
| **SyncDirection** | Push, Pull, Bidirectional |
| **WebhookEventType** | invoice.received, invoice.extracted, invoice.approved, invoice.compliant, invoice.transmitted, invoice.failed |

---

## 6. API Layer

### .NET API Endpoints (25+ routes)

| Prefix | Auth | Description |
|--------|------|-------------|
| `POST /api/auth/login` | Anonymous | JWT login |
| `POST /api/auth/register` | Anonymous | User registration |
| `POST /api/auth/refresh` | Anonymous | Token refresh |
| `POST /api/auth/logout` | JWT | Revoke refresh token |
| `GET /api/auth/me` | JWT | Current user info |
| `POST /api/ingestion/upload` | JWT | Multipart file upload |
| `POST /api/ingestion/webhook` | HMAC | Webhook push endpoint |
| `GET/POST /api/credit-notes` | JWT | Credit note CRUD |
| `GET/POST /api/debit-notes` | JWT | Debit note CRUD |
| `GET/POST /api/purchase-orders` | JWT | Purchase order CRUD |
| `GET/POST /api/delivery-notes` | JWT | Delivery note CRUD |
| `GET/POST /api/reminders` | JWT | Reminder CRUD + send/escalate |
| `GET /api/documents` | JWT | Document list + search |
| `GET /api/export/invoices/csv` | JWT | CSV download |
| `GET /api/export/invoices/excel` | JWT | TSV download |
| `GET/POST /api/approval/chains` | Admin | Chain management |
| `POST /api/approval/invoices/{id}/start` | JWT | Start approval workflow |
| `POST /api/approval/requests/{id}/approve` | JWT | Approve step |
| `POST /api/approval/requests/{id}/reject` | JWT | Reject step |
| `GET /api/versions` | Any | API version discovery |
| `GET /health` | Any | Health check |
| `GET /metrics` | Internal | Prometheus metrics |
| `GET /swagger` | Dev | Swagger UI |

### Python FastAPI Endpoints (28+ routes)

| Prefix | Description |
|--------|-------------|
| `GET/POST /api/v1/auth` | Login, register |
| `GET/POST /api/v1/invoices` | Invoice CRUD |
| `GET/POST /api/v1/ingestion` | Upload, stats, poll |
| `POST /api/v1/compliance` | Validate, process, transmit, archive |
| `GET /api/v1/analytics` | Dashboard stats, charts |
| `GET/PUT /api/v1/settings` | Tenant settings |
| `GET/POST /api/v1/connectors` | Connector management + sync |
| `GET/POST /api/v1/webhooks` | Webhook management |

---

## 7. Feature Coverage

### All 14 Implemented Features

| # | Feature | Status | Layer | Key Files |
|---|---------|--------|-------|-----------|
| 1 | **NuGet Vulnerability Fix** | ✅ | Build | `NuGet.config`, `Directory.Build.props` |
| 2 | **CI/CD Pipeline** | ✅ | DevOps | `.github/workflows/ci.yml` |
| 3 | **SignalR Dashboard** | ✅ | .NET + Frontend | `InvoiceHub.cs`, `signalr.ts`, `page.tsx` |
| 4 | **RBAC** | ✅ | .NET | `RbacPolicies.cs`, `DependencyInjection.cs` |
| 5 | **Approval Workflow** | ✅ | .NET | `ApprovalChain/Step.cs`, `WorkflowService.cs`, `ApprovalEndpoints.cs` |
| 6 | **Bulk Compliance** | ✅ | Frontend | `BulkActionBar.tsx`, `invoices/page.tsx` |
| 7 | **Document Annotations** | ✅ | Frontend | `DocumentAnnotations.tsx` |
| 8 | **CSV/Excel Export** | ✅ | .NET | `ExportEndpoints.cs` |
| 9 | **Email Notifications** | ✅ | .NET | `EmailNotificationService.cs` |
| 10 | **i18n / Multi-language** | ✅ | Frontend | `i18n.tsx` (8 languages) |
| 11 | **PDF Generation** | ✅ | .NET | `InvoicePdfGenerationService.cs` |
| 12 | **Webhook Backoff** | ✅ | .NET | `WebhookDeliveryService.cs` |
| 13 | **API Versioning** | ✅ | .NET | `ApiVersioningMiddleware.cs` |
| 14 | **API Key Management** | ✅ | .NET | `ApiKeyAuthHandler.cs`, `ApiKey.cs` |

---

## 8. Security & Compliance

### Multi-Tenant Isolation

```
┌─────────────────────────────────────────────┐
│  Global Query Filters (EF Core)             │
│                                             │
│  Every query automatically appends:        │
│    WHERE tenant_id = @CurrentTenantId       │
│                                             │
│  Applied to: Invoice, Document, User,       │
│  ComplianceConfig, ConnectorConfig,         │
│  WebhookConfig, ApprovalRequest, AuditLog,  │
│  ApprovalChain, ApprovalStep, ApiKey        │
└─────────────────────────────────────────────┘
```

### Authentication Flow

```
┌──────────┐     ┌──────────────┐     ┌────────────┐
│  Client  │────▶│  /api/auth/  │────▶│  JWT       │
│          │     │  login       │     │  Issued    │
└──────────┘     └──────────────┘     └─────┬──────┘
                                            │
                    ┌───────────────────────┼──────────────┐
                    │                       │              │
            ┌───────▼───────┐     ┌─────────▼────────┐    │
            │  JWT Bearer   │     │  API Key Auth    │    │
            │  (User-facing)│     │  (Machine-facing)│    │
            └───────┬───────┘     └─────────┬────────┘    │
                    │                       │              │
            ┌───────▼───────────────────────▼────────┐    │
            │  RBAC Authorization Policies            │    │
            │  RequireAdmin / RequireApprover /       │    │
            │  RequireViewer / RequireComplianceAccess│    │
            │  RequireConnectorManagement             │    │
            └─────────────────────────────────────────┘    │
```

### Compliance Models Coverage

```
┌─────────────────────────────────────────────────────────┐
│  COMPLIANCE ORCHESTRATOR                                   │
│  Routes invoice to correct handler by country/model       │
│                                                           │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐ │
│  │ PEPPOL   │  │ ZATCA    │  │ Brazil   │  │ India    │ │
│  │ (EU/EEA) │  │ (KSA)    │  │ NF-e     │  │ IRP      │ │
│  │ Validate │  │ Clearance│  │ Submit   │  │ Submit   │ │
│  │ +Transmit│  │ Request  │  │ +Status  │  │ +Status  │ │
│  └──────────┘  └──────────┘  └──────────┘  └──────────┘ │
│                                                           │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐ │
│  │ Mexico   │  │ Italy    │  │ France   │  │ Poland   │ │
│  │ CFDI     │  │ SdI      │  │ PPF      │  │ KSeF     │ │
│  │ Stamp    │  │ FatturaPA│  │ Report   │  │ Report   │ │
│  │ +Status  │  │ +Status  │  │ +Status  │  │ +Status  │ │
│  └──────────┘  └──────────┘  └──────────┘  └──────────┘ │
│                                                           │
│  ┌──────────┐                                            │
│  │Post-Audit│  Fallback for unsupported countries         │
│  │ Archive  │  SHA-256 hash + immutable record            │
│  └──────────┘                                            │
└───────────────────────────────────────────────────────────┘
```

### Webhook Security

- **HMAC-SHA256 signing** of all webhook payloads
- **Exponential backoff** with jitter (1s, 2s, 4s, 8s, 16s, 32s)
- **Max retries** configurable per webhook (default: 3)
- **Timeout** configurable per webhook (default: 30s)
- **Signature header**: `X-InvoiceFlow-Signature: sha256=<hash>`
- **Timestamp header**: `X-InvoiceFlow-Timestamp: <unix_epoch>`

---

## 9. Deployment Architecture

### Docker Compose Services (Production)

```
┌─────────────────────────────────────────────────────────────┐
│                    docker-compose.prod.yml                    │
│                                                              │
│  ┌─────────┐  ┌──────────────┐  ┌─────────────────────┐    │
│  │ Backend │  │  Frontend    │  │  Celery Workers *4  │    │
│  │ (FastAPI)│  │  (Next.js)  │  │  Ingestion          │    │
│  │ Port 8000│  │  Port 3000  │  │  Extraction          │    │
│  └────┬────┘  └──────┬───────┘  │  Compliance          │    │
│       │              │          │  Webhooks            │    │
│       │              │          └─────────┬────────────┘    │
│       └──────────────┼────────────────────┘                 │
│                      │                                      │
│  ┌───────────────────┴───────────────────────────────────┐  │
│  │  Shared Infrastructure                                │  │
│  │  ┌──────────┐ ┌──────┐ ┌────────┐ ┌──────────────┐  │  │
│  │  │PostgreSQL│ │Redis │ │ MinIO  │ │  RabbitMQ    │  │  │
│  │  │ 16 Alpine│ │ 7    │ │ S3     │ │  3.13        │  │  │
│  │  └──────────┘ └──────┘ └────────┘ └──────────────┘  │  │
│  └──────────────────────────────────────────────────────┘  │
│                                                              │
│  ┌──────────────────────────────────────────────────────┐   │
│  │  Monitoring                                          │   │
│  │  ┌───────────┐ ┌────────┐ ┌─────────────────────┐   │   │
│  │  │Prometheus │ │Grafana │ │  Prometheus + Node   │   │   │
│  │  │ Port 9090 │ │Port    │ │  Exporter + cAdvisor │   │   │
│  │  └───────────┘ │ 3001   │ └─────────────────────┘   │   │
│  │                └────────┘                           │   │
│  └──────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────┘
```

### Resource Allocation (Production)

| Service | Memory | CPU | Replicas |
|---------|--------|-----|----------|
| Backend API | 1GB | 1.0 | 2+ |
| Celery (ingestion+extraction) | 1GB | 1.0 | 2+ |
| Celery (compliance) | 512MB | 0.5 | 2+ |
| Celery (webhooks) | 256MB | 0.25 | 1 |
| Frontend | 256MB | 0.5 | 2+ |
| PostgreSQL | 2GB | — | 1 |
| Redis | 512MB | — | 1 |
| MinIO | 1GB | — | 1 |
| Prometheus | 256MB | — | 1 |
| Grafana | 256MB | — | 1 |

### Environment Configuration

See `production.env.example` for the complete list of ~50 environment variables covering:

- Database connection (DATABASE_URL)
- Redis/Celery (REDIS_URL, CELERY_BROKER_URL)
- MinIO credentials (MINIO_ACCESS_KEY, MINIO_SECRET_KEY)
- JWT secrets (JWT_SECRET_KEY)
- LLM API keys (ANTHROPIC_API_KEY, OPENAI_API_KEY)
- IMAP/FTP credentials for ingestion
- Compliance API keys (ZATCA, Brazil, India, Mexico)
- ERP connector credentials (Xero, SAP, Oracle)

---

*Generated: July 2026 | InvoiceFlow v1.0*
*For the complete API reference, see `/swagger` or `frontend/src/lib/openapi.json`*
