using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvoiceFlow.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DocumentLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    LineNumber = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    ProductCode = table.Column<string>(type: "text", nullable: true),
                    HsnCode = table.Column<string>(type: "text", nullable: true),
                    Quantity = table.Column<decimal>(type: "numeric", nullable: false),
                    Unit = table.Column<string>(type: "text", nullable: true),
                    UnitPrice = table.Column<decimal>(type: "numeric", nullable: false),
                    LineTotal = table.Column<decimal>(type: "numeric", nullable: false),
                    TaxRate = table.Column<decimal>(type: "numeric", nullable: false),
                    TaxAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    TaxCategory = table.Column<string>(type: "text", nullable: true),
                    DiscountPercent = table.Column<decimal>(type: "numeric", nullable: true),
                    DiscountAmount = table.Column<decimal>(type: "numeric", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentLines", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tenants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TaxId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Country = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "compliance_configs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    Model = table.Column<int>(type: "integer", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    SandboxMode = table.Column<bool>(type: "boolean", nullable: false),
                    ConfigJson = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_compliance_configs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_compliance_configs_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "connector_configs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConnectorType = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CredentialsJson = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    SandboxMode = table.Column<bool>(type: "boolean", nullable: false),
                    SyncDirection = table.Column<int>(type: "integer", nullable: false),
                    ExtraConfigJson = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    SyncIntervalMinutes = table.Column<int>(type: "integer", nullable: true),
                    LastSyncAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TotalSynced = table.Column<int>(type: "integer", nullable: true),
                    FailedSyncs = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_connector_configs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_connector_configs_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "invoices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DocumentType = table.Column<int>(type: "integer", nullable: false),
                    InvoiceDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    VendorName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    VendorTaxId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    VendorEmail = table.Column<string>(type: "text", nullable: true),
                    BuyerName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    BuyerTaxId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Subtotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TaxAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    ShippingAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    ExtractionMethod = table.Column<int>(type: "integer", nullable: true),
                    OcrConfidence = table.Column<double>(type: "double precision", nullable: true),
                    CountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    ComplianceModel = table.Column<int>(type: "integer", nullable: true),
                    ComplianceId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ComplianceResponse = table.Column<string>(type: "text", nullable: true),
                    OriginalFileName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    StoragePath = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    MimeType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ReferenceNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    ErpId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExtractedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompliantAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TransmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_invoices_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "purchase_orders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DocumentDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IssuerName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    IssuerTaxId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    IssuerEmail = table.Column<string>(type: "text", nullable: true),
                    IssuerAddress = table.Column<string>(type: "text", nullable: true),
                    RecipientName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    RecipientTaxId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    RecipientEmail = table.Column<string>(type: "text", nullable: true),
                    RecipientAddress = table.Column<string>(type: "text", nullable: true),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Subtotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TaxAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    ShippingAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    ExtractionMethod = table.Column<int>(type: "integer", nullable: true),
                    OcrConfidence = table.Column<double>(type: "double precision", nullable: true),
                    CountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    ComplianceModel = table.Column<int>(type: "integer", nullable: true),
                    ComplianceId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ComplianceResponse = table.Column<string>(type: "text", nullable: true),
                    OriginalFileName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    StoragePath = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    MimeType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ReferenceNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    ErpId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExtractedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompliantAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TransmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExpectedDeliveryDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeliveryAddress = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    PaymentTerms = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Incoterms = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ShipToName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ShipToAddress = table.Column<string>(type: "text", nullable: true),
                    BillToName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    BillToAddress = table.Column<string>(type: "text", nullable: true),
                    ContactName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ContactEmail = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    ContactPhone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_orders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_purchase_orders_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastLoginAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_users_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "webhook_configs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Url = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Secret = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Events = table.Column<string>(type: "jsonb", nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TimeoutSeconds = table.Column<int>(type: "integer", nullable: false),
                    MaxRetries = table.Column<int>(type: "integer", nullable: false),
                    SuccessCount = table.Column<int>(type: "integer", nullable: true),
                    FailureCount = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastTriggeredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_webhook_configs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_webhook_configs_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: true),
                    Action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PerformedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Details = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    PreviousHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CurrentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_logs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_audit_logs_invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "credit_notes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DocumentDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IssuerName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    IssuerTaxId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    IssuerEmail = table.Column<string>(type: "text", nullable: true),
                    IssuerAddress = table.Column<string>(type: "text", nullable: true),
                    RecipientName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    RecipientTaxId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    RecipientEmail = table.Column<string>(type: "text", nullable: true),
                    RecipientAddress = table.Column<string>(type: "text", nullable: true),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Subtotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TaxAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    ShippingAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    ExtractionMethod = table.Column<int>(type: "integer", nullable: true),
                    OcrConfidence = table.Column<double>(type: "double precision", nullable: true),
                    CountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    ComplianceModel = table.Column<int>(type: "integer", nullable: true),
                    ComplianceId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ComplianceResponse = table.Column<string>(type: "text", nullable: true),
                    OriginalFileName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    StoragePath = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    MimeType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ReferenceNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    ErpId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExtractedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompliantAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TransmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    OriginalInvoiceId = table.Column<Guid>(type: "uuid", nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_credit_notes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_credit_notes_invoices_OriginalInvoiceId",
                        column: x => x.OriginalInvoiceId,
                        principalTable: "invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_credit_notes_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "debit_notes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DocumentDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IssuerName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    IssuerTaxId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    IssuerEmail = table.Column<string>(type: "text", nullable: true),
                    IssuerAddress = table.Column<string>(type: "text", nullable: true),
                    RecipientName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    RecipientTaxId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    RecipientEmail = table.Column<string>(type: "text", nullable: true),
                    RecipientAddress = table.Column<string>(type: "text", nullable: true),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Subtotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TaxAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    ShippingAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    ExtractionMethod = table.Column<int>(type: "integer", nullable: true),
                    OcrConfidence = table.Column<double>(type: "double precision", nullable: true),
                    CountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    ComplianceModel = table.Column<int>(type: "integer", nullable: true),
                    ComplianceId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ComplianceResponse = table.Column<string>(type: "text", nullable: true),
                    OriginalFileName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    StoragePath = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    MimeType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ReferenceNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    ErpId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExtractedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompliantAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TransmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    OriginalInvoiceId = table.Column<Guid>(type: "uuid", nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_debit_notes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_debit_notes_invoices_OriginalInvoiceId",
                        column: x => x.OriginalInvoiceId,
                        principalTable: "invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_debit_notes_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "invoice_lines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    LineNumber = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ProductCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    HsnCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Quantity = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Unit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    LineTotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TaxRate = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TaxAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TaxCategory = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    DiscountPercent = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    DiscountAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invoice_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_invoice_lines_invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "reminders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DocumentDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IssuerName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    IssuerTaxId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    IssuerEmail = table.Column<string>(type: "text", nullable: true),
                    IssuerAddress = table.Column<string>(type: "text", nullable: true),
                    RecipientName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    RecipientTaxId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    RecipientEmail = table.Column<string>(type: "text", nullable: true),
                    RecipientAddress = table.Column<string>(type: "text", nullable: true),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Subtotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TaxAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    ShippingAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    ExtractionMethod = table.Column<int>(type: "integer", nullable: true),
                    OcrConfidence = table.Column<double>(type: "double precision", nullable: true),
                    CountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    ComplianceModel = table.Column<int>(type: "integer", nullable: true),
                    ComplianceId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ComplianceResponse = table.Column<string>(type: "text", nullable: true),
                    OriginalFileName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    StoragePath = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    MimeType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ReferenceNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    ErpId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExtractedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompliantAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TransmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReminderLevel = table.Column<int>(type: "integer", nullable: false),
                    DaysOverdue = table.Column<int>(type: "integer", nullable: false),
                    ReminderFee = table.Column<decimal>(type: "numeric", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AcknowledgedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reminders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_reminders_invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_reminders_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "delivery_notes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DocumentDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IssuerName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    IssuerTaxId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    IssuerEmail = table.Column<string>(type: "text", nullable: true),
                    IssuerAddress = table.Column<string>(type: "text", nullable: true),
                    RecipientName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    RecipientTaxId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    RecipientEmail = table.Column<string>(type: "text", nullable: true),
                    RecipientAddress = table.Column<string>(type: "text", nullable: true),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Subtotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TaxAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    ShippingAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    ExtractionMethod = table.Column<int>(type: "integer", nullable: true),
                    OcrConfidence = table.Column<double>(type: "double precision", nullable: true),
                    CountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    ComplianceModel = table.Column<int>(type: "integer", nullable: true),
                    ComplianceId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ComplianceResponse = table.Column<string>(type: "text", nullable: true),
                    OriginalFileName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    StoragePath = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    MimeType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ReferenceNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    ErpId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExtractedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompliantAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TransmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    PurchaseOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeliveryAddress = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CarrierName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    TrackingNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DeliveredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReceivedBy = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    DeliveredQuantity = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    SignaturePath = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ProofOfDeliveryPath = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReceiverSignature = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_delivery_notes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_delivery_notes_purchase_orders_PurchaseOrderId",
                        column: x => x.PurchaseOrderId,
                        principalTable: "purchase_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_delivery_notes_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "approval_requests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Comments = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    AssignedToUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_approval_requests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_approval_requests_invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_approval_requests_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_approval_requests_users_AssignedToUserId",
                        column: x => x.AssignedToUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_approval_requests_users_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Token = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refresh_tokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_refresh_tokens_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    MimeType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    StoragePath = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    OcrText = table.Column<string>(type: "character varying(50000)", maxLength: 50000, nullable: true),
                    Tags = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Folder = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    IsLatestVersion = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    Checksum = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    PageCount = table.Column<int>(type: "integer", nullable: true),
                    ThumbnailPath = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    LinkedInvoiceId = table.Column<Guid>(type: "uuid", nullable: true),
                    LinkedCreditNoteId = table.Column<Guid>(type: "uuid", nullable: true),
                    LinkedDebitNoteId = table.Column<Guid>(type: "uuid", nullable: true),
                    LinkedPurchaseOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    LinkedDeliveryNoteId = table.Column<Guid>(type: "uuid", nullable: true),
                    LinkedReminderId = table.Column<Guid>(type: "uuid", nullable: true),
                    OriginalDocumentId = table.Column<Guid>(type: "uuid", nullable: true),
                    DocumentType = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_documents_credit_notes_LinkedCreditNoteId",
                        column: x => x.LinkedCreditNoteId,
                        principalTable: "credit_notes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_documents_debit_notes_LinkedDebitNoteId",
                        column: x => x.LinkedDebitNoteId,
                        principalTable: "debit_notes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_documents_delivery_notes_LinkedDeliveryNoteId",
                        column: x => x.LinkedDeliveryNoteId,
                        principalTable: "delivery_notes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_documents_documents_OriginalDocumentId",
                        column: x => x.OriginalDocumentId,
                        principalTable: "documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_documents_invoices_LinkedInvoiceId",
                        column: x => x.LinkedInvoiceId,
                        principalTable: "invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_documents_purchase_orders_LinkedPurchaseOrderId",
                        column: x => x.LinkedPurchaseOrderId,
                        principalTable: "purchase_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_documents_reminders_LinkedReminderId",
                        column: x => x.LinkedReminderId,
                        principalTable: "reminders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_documents_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "document_relationships",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceDocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetDocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    RelationshipType = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Metadata = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TargetTenantId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_relationships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_document_relationships_documents_SourceDocumentId",
                        column: x => x.SourceDocumentId,
                        principalTable: "documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_document_relationships_documents_TargetDocumentId",
                        column: x => x.TargetDocumentId,
                        principalTable: "documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_document_relationships_tenants_TargetTenantId",
                        column: x => x.TargetTenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_document_relationships_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_document_relationships_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "document_version_histories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    ChangeType = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ChangeDetails = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    OldValue = table.Column<string>(type: "text", nullable: true),
                    NewValue = table.Column<string>(type: "text", nullable: true),
                    FieldName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ChangedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ChangedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_version_histories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_document_version_histories_documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_document_version_histories_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_document_version_histories_users_ChangedByUserId",
                        column: x => x.ChangedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_approval_requests_AssignedToUserId",
                table: "approval_requests",
                column: "AssignedToUserId");

            migrationBuilder.CreateIndex(
                name: "IX_approval_requests_InvoiceId",
                table: "approval_requests",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_approval_requests_ReviewedByUserId",
                table: "approval_requests",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_approval_requests_tenant_status",
                table: "approval_requests",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_created_at",
                table: "audit_logs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_InvoiceId",
                table: "audit_logs",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_tenant_action",
                table: "audit_logs",
                columns: new[] { "TenantId", "Action" });

            migrationBuilder.CreateIndex(
                name: "IX_compliance_configs_tenant_country_model",
                table: "compliance_configs",
                columns: new[] { "TenantId", "CountryCode", "Model" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_connector_configs_tenant_type",
                table: "connector_configs",
                columns: new[] { "TenantId", "ConnectorType" });

            migrationBuilder.CreateIndex(
                name: "IX_credit_notes_deleted_by_user",
                table: "credit_notes",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_credit_notes_original_invoice",
                table: "credit_notes",
                column: "OriginalInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_credit_notes_tenant_date",
                table: "credit_notes",
                columns: new[] { "TenantId", "DocumentDate" });

            migrationBuilder.CreateIndex(
                name: "IX_credit_notes_tenant_deleted",
                table: "credit_notes",
                columns: new[] { "TenantId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_credit_notes_tenant_number",
                table: "credit_notes",
                columns: new[] { "TenantId", "DocumentNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_credit_notes_TenantId",
                table: "credit_notes",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_debit_notes_deleted_by_user",
                table: "debit_notes",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_debit_notes_original_invoice",
                table: "debit_notes",
                column: "OriginalInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_debit_notes_tenant_date",
                table: "debit_notes",
                columns: new[] { "TenantId", "DocumentDate" });

            migrationBuilder.CreateIndex(
                name: "IX_debit_notes_tenant_deleted",
                table: "debit_notes",
                columns: new[] { "TenantId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_debit_notes_tenant_number",
                table: "debit_notes",
                columns: new[] { "TenantId", "DocumentNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_debit_notes_TenantId",
                table: "debit_notes",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_delivery_notes_deleted_by_user",
                table: "delivery_notes",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_delivery_notes_purchase_order",
                table: "delivery_notes",
                column: "PurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_delivery_notes_tenant_date",
                table: "delivery_notes",
                columns: new[] { "TenantId", "DocumentDate" });

            migrationBuilder.CreateIndex(
                name: "IX_delivery_notes_tenant_deleted",
                table: "delivery_notes",
                columns: new[] { "TenantId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_delivery_notes_tenant_number",
                table: "delivery_notes",
                columns: new[] { "TenantId", "DocumentNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_delivery_notes_TenantId",
                table: "delivery_notes",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_delivery_notes_tracking",
                table: "delivery_notes",
                column: "TrackingNumber");

            migrationBuilder.CreateIndex(
                name: "IX_document_relationships_created_at",
                table: "document_relationships",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_document_relationships_created_by_user",
                table: "document_relationships",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_document_relationships_source_target_type",
                table: "document_relationships",
                columns: new[] { "SourceDocumentId", "TargetDocumentId", "RelationshipType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_document_relationships_TargetDocumentId",
                table: "document_relationships",
                column: "TargetDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_document_relationships_TargetTenantId",
                table: "document_relationships",
                column: "TargetTenantId");

            migrationBuilder.CreateIndex(
                name: "IX_document_relationships_tenant_source",
                table: "document_relationships",
                columns: new[] { "TenantId", "SourceDocumentId" });

            migrationBuilder.CreateIndex(
                name: "IX_document_relationships_tenant_target",
                table: "document_relationships",
                columns: new[] { "TenantId", "TargetDocumentId" });

            migrationBuilder.CreateIndex(
                name: "IX_document_relationships_type",
                table: "document_relationships",
                column: "RelationshipType");

            migrationBuilder.CreateIndex(
                name: "IX_document_version_histories_change_type",
                table: "document_version_histories",
                column: "ChangeType");

            migrationBuilder.CreateIndex(
                name: "IX_document_version_histories_ChangedByUserId",
                table: "document_version_histories",
                column: "ChangedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_document_version_histories_created_at",
                table: "document_version_histories",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_document_version_histories_document_field",
                table: "document_version_histories",
                columns: new[] { "DocumentId", "FieldName" });

            migrationBuilder.CreateIndex(
                name: "IX_document_version_histories_document_version",
                table: "document_version_histories",
                columns: new[] { "DocumentId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_document_version_histories_tenant_document",
                table: "document_version_histories",
                columns: new[] { "TenantId", "DocumentId" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentLines_DocumentId",
                table: "DocumentLines",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_documents_linked_credit_note",
                table: "documents",
                column: "LinkedCreditNoteId");

            migrationBuilder.CreateIndex(
                name: "IX_documents_linked_debit_note",
                table: "documents",
                column: "LinkedDebitNoteId");

            migrationBuilder.CreateIndex(
                name: "IX_documents_linked_delivery_note",
                table: "documents",
                column: "LinkedDeliveryNoteId");

            migrationBuilder.CreateIndex(
                name: "IX_documents_linked_invoice",
                table: "documents",
                column: "LinkedInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_documents_linked_purchase_order",
                table: "documents",
                column: "LinkedPurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_documents_linked_reminder",
                table: "documents",
                column: "LinkedReminderId");

            migrationBuilder.CreateIndex(
                name: "IX_documents_original_document",
                table: "documents",
                column: "OriginalDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_documents_tenant_checksum",
                table: "documents",
                columns: new[] { "TenantId", "Checksum" },
                filter: "checksum IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_documents_tenant_latest",
                table: "documents",
                columns: new[] { "TenantId", "IsLatestVersion" });

            migrationBuilder.CreateIndex(
                name: "IX_documents_tenant_original_version",
                table: "documents",
                columns: new[] { "TenantId", "OriginalDocumentId", "Version" });

            migrationBuilder.CreateIndex(
                name: "IX_documents_tenant_type",
                table: "documents",
                columns: new[] { "TenantId", "DocumentType" });

            migrationBuilder.CreateIndex(
                name: "IX_invoice_lines_invoice_linenum",
                table: "invoice_lines",
                columns: new[] { "InvoiceId", "LineNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_invoices_tenant_date",
                table: "invoices",
                columns: new[] { "TenantId", "InvoiceDate" });

            migrationBuilder.CreateIndex(
                name: "IX_invoices_tenant_number",
                table: "invoices",
                columns: new[] { "TenantId", "InvoiceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_invoices_tenant_status",
                table: "invoices",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_orders_deleted_by_user",
                table: "purchase_orders",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_orders_expected_delivery",
                table: "purchase_orders",
                column: "ExpectedDeliveryDate");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_orders_tenant_date",
                table: "purchase_orders",
                columns: new[] { "TenantId", "DocumentDate" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_orders_tenant_deleted",
                table: "purchase_orders",
                columns: new[] { "TenantId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_orders_tenant_number",
                table: "purchase_orders",
                columns: new[] { "TenantId", "DocumentNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchase_orders_tenant_status",
                table: "purchase_orders",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_orders_TenantId",
                table: "purchase_orders",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_token",
                table: "refresh_tokens",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_user_id",
                table: "refresh_tokens",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_user_revoked",
                table: "refresh_tokens",
                columns: new[] { "UserId", "RevokedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_reminders_deleted_by_user",
                table: "reminders",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_reminders_due_date",
                table: "reminders",
                column: "DueDate");

            migrationBuilder.CreateIndex(
                name: "IX_reminders_invoice",
                table: "reminders",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_reminders_level",
                table: "reminders",
                column: "ReminderLevel");

            migrationBuilder.CreateIndex(
                name: "IX_reminders_tenant_date",
                table: "reminders",
                columns: new[] { "TenantId", "DocumentDate" });

            migrationBuilder.CreateIndex(
                name: "IX_reminders_tenant_deleted",
                table: "reminders",
                columns: new[] { "TenantId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_reminders_tenant_number",
                table: "reminders",
                columns: new[] { "TenantId", "DocumentNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_reminders_tenant_status",
                table: "reminders",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_reminders_TenantId",
                table: "reminders",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_tenants_Slug",
                table: "tenants",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_tenant_email",
                table: "users",
                columns: new[] { "TenantId", "Email" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_webhook_configs_tenant_name",
                table: "webhook_configs",
                columns: new[] { "TenantId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "approval_requests");

            migrationBuilder.DropTable(
                name: "audit_logs");

            migrationBuilder.DropTable(
                name: "compliance_configs");

            migrationBuilder.DropTable(
                name: "connector_configs");

            migrationBuilder.DropTable(
                name: "document_relationships");

            migrationBuilder.DropTable(
                name: "document_version_histories");

            migrationBuilder.DropTable(
                name: "DocumentLines");

            migrationBuilder.DropTable(
                name: "invoice_lines");

            migrationBuilder.DropTable(
                name: "refresh_tokens");

            migrationBuilder.DropTable(
                name: "webhook_configs");

            migrationBuilder.DropTable(
                name: "documents");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "credit_notes");

            migrationBuilder.DropTable(
                name: "debit_notes");

            migrationBuilder.DropTable(
                name: "delivery_notes");

            migrationBuilder.DropTable(
                name: "reminders");

            migrationBuilder.DropTable(
                name: "purchase_orders");

            migrationBuilder.DropTable(
                name: "invoices");

            migrationBuilder.DropTable(
                name: "tenants");
        }
    }
}
