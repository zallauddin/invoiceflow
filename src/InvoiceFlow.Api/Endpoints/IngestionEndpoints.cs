using InvoiceFlow.Core.Entities;
using InvoiceFlow.Core.Enums;
using InvoiceFlow.Core.Interfaces;
using InvoiceFlow.Core.Messages;
using MassTransit;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace InvoiceFlow.Api.Endpoints;

/// <summary>
/// File ingestion endpoints: multipart upload, webhook push, validation.
/// </summary>
public static class IngestionEndpoints
{
    /// <summary>Default storage bucket for uploaded documents.</summary>
    private const string DocumentsBucket = "documents";

    /// <summary>Maximum upload file size: 50 MB.</summary>
    private const long MaxFileSize = 50L * 1024 * 1024;

    /// <summary>File extensions accepted for upload.</summary>
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".xml", ".jpg", ".jpeg", ".png", ".tiff", ".tif"
    };

    public static WebApplication MapIngestionEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/ingestion")
            .WithTags("Ingestion")
            .RequireAuthorization();

        // POST /api/ingestion/upload — Multipart file upload
        group.MapPost("/upload", async (
            HttpRequest request,
            [FromServices] IStorageService storageService,
            [FromServices] IRepository<Document> documentRepository,
            [FromServices] IRepository<Invoice> invoiceRepository,
            [FromServices] IPublishEndpoint publishEndpoint,
            [FromServices] ITenantIdProvider tenantIdProvider,
            [FromServices] ILogger<Program> logger,
            CancellationToken cancellationToken) =>
        {
            // Validate content type
            if (!request.HasFormContentType || request.Form.Files.Count == 0)
            {
                return Results.BadRequest(new { error = "No file uploaded. Use multipart/form-data." });
            }

            var file = request.Form.Files[0];
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

            if (!AllowedExtensions.Contains(extension))
            {
                return Results.BadRequest(new { error = $"File type {extension} not allowed. Allowed: {string.Join(", ", AllowedExtensions)}" });
            }

            if (file.Length > MaxFileSize)
            {
                return Results.BadRequest(new { error = "File size exceeds 50MB limit." });
            }

            var tenantId = tenantIdProvider.TenantId;
            if (tenantId is null || tenantId.Value == Guid.Empty)
            {
                return Results.BadRequest(new { error = "Tenant not resolved." });
            }

            // Generate storage path: tenant/category/date/filename
            var datePath = DateTime.UtcNow.ToString("yyyy/MM");
            var storagePath = $"{tenantId.Value}/ingestion/{datePath}/{Guid.NewGuid()}{extension}";

            // Upload to MinIO
            using var stream = file.OpenReadStream();
            await storageService.UploadAsync(DocumentsBucket, storagePath, stream, file.ContentType, cancellationToken);

            // Create Document entity
            var document = new Document
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId.Value,
                FileName = file.FileName,
                MimeType = file.ContentType,
                FileSize = file.Length,
                StoragePath = storagePath,
                DocumentType = DetectDocumentType(extension),
                CreatedAt = DateTime.UtcNow
            };
            await documentRepository.AddAsync(document, cancellationToken);

            // Create Invoice entity (Draft status, will be updated after extraction)
            var invoice = new Invoice
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId.Value,
                DocumentType = document.DocumentType,
                Status = InvoiceStatus.Received,
                Source = IngestionSource.ApiUpload,
                OriginalFileName = file.FileName,
                StoragePath = storagePath,
                MimeType = file.ContentType,
                CreatedAt = DateTime.UtcNow
            };
            await invoiceRepository.AddAsync(invoice, cancellationToken);

            // Link document to invoice
            document.LinkedInvoiceId = invoice.Id;
            await documentRepository.UpdateAsync(document, cancellationToken);

            // Publish extraction command
            await publishEndpoint.Publish(new ExtractInvoiceCommand
            {
                InvoiceId = invoice.Id,
                TenantId = tenantId.Value,
                DocumentId = document.Id,
                StoragePath = storagePath,
                MimeType = file.ContentType,
                FileName = file.FileName
            }, cancellationToken);

            logger.LogInformation("File uploaded: {FileName} -> InvoiceId: {InvoiceId}", file.FileName, invoice.Id);

            return Results.Accepted($"/api/invoices/{invoice.Id}", new
            {
                invoiceId = invoice.Id,
                documentId = document.Id,
                status = "Accepted for processing",
                message = "File uploaded successfully. Extraction started."
            });
        })
        .WithName("UploadFile")
        .WithSummary("Upload invoice file for processing")
        .DisableAntiforgery();

        // POST /api/ingestion/webhook — Webhook push endpoint
        group.MapPost("/webhook", async (
            HttpRequest request,
            [FromServices] IStorageService storageService,
            [FromServices] IRepository<Document> documentRepository,
            [FromServices] IRepository<Invoice> invoiceRepository,
            [FromServices] IPublishEndpoint publishEndpoint,
            [FromServices] ITenantIdProvider tenantIdProvider,
            [FromServices] ILogger<Program> logger,
            CancellationToken cancellationToken) =>
        {
            // Validate webhook signature (HMAC-SHA256) — implementation depends on provider
            // For now, accept any webhook with file content

            if (!request.HasFormContentType && request.ContentLength == 0)
            {
                return Results.BadRequest(new { error = "No content provided." });
            }

            var tenantId = tenantIdProvider.TenantId;
            if (tenantId is null || tenantId.Value == Guid.Empty)
            {
                return Results.BadRequest(new { error = "Tenant not resolved." });
            }

            IFormFile? file = null;
            if (request.HasFormContentType && request.Form.Files.Count > 0)
            {
                file = request.Form.Files[0];
            }
            else if (request.ContentLength > 0)
            {
                // Raw body as file — not yet supported
                return Results.BadRequest(new { error = "Raw body upload not yet supported. Use multipart/form-data." });
            }

            if (file == null)
            {
                return Results.BadRequest(new { error = "No file found in request." });
            }

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            var datePath = DateTime.UtcNow.ToString("yyyy/MM");
            var storagePath = $"{tenantId.Value}/ingestion/{datePath}/{Guid.NewGuid()}{extension}";

            using var stream = file.OpenReadStream();
            await storageService.UploadAsync(DocumentsBucket, storagePath, stream, file.ContentType, cancellationToken);

            var document = new Document
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId.Value,
                FileName = file.FileName,
                MimeType = file.ContentType,
                FileSize = file.Length,
                StoragePath = storagePath,
                DocumentType = DetectDocumentType(extension),
                CreatedAt = DateTime.UtcNow
            };
            await documentRepository.AddAsync(document, cancellationToken);

            var invoice = new Invoice
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId.Value,
                DocumentType = document.DocumentType,
                Status = InvoiceStatus.Received,
                Source = IngestionSource.Webhook,
                OriginalFileName = file.FileName,
                StoragePath = storagePath,
                MimeType = file.ContentType,
                CreatedAt = DateTime.UtcNow
            };
            await invoiceRepository.AddAsync(invoice, cancellationToken);

            document.LinkedInvoiceId = invoice.Id;
            await documentRepository.UpdateAsync(document, cancellationToken);

            await publishEndpoint.Publish(new ExtractInvoiceCommand
            {
                InvoiceId = invoice.Id,
                TenantId = tenantId.Value,
                DocumentId = document.Id,
                StoragePath = storagePath,
                MimeType = file.ContentType,
                FileName = file.FileName
            }, cancellationToken);

            logger.LogInformation("Webhook received: {FileName} -> InvoiceId: {InvoiceId}", file.FileName, invoice.Id);

            return Results.Accepted($"/api/invoices/{invoice.Id}", new
            {
                invoiceId = invoice.Id,
                documentId = document.Id,
                status = "Accepted for processing",
                message = "Webhook file received. Extraction started."
            });
        })
        .WithName("WebhookPush")
        .WithSummary("Receive invoice via webhook")
        .AllowAnonymous(); // Webhooks typically use HMAC validation instead of JWT

        return app;
    }

    private static DocumentType DetectDocumentType(string extension)
    {
        return extension switch
        {
            ".xml" => DocumentType.Invoice, // Could be UBL, etc.
            ".pdf" => DocumentType.Invoice,
            _ => DocumentType.Invoice
        };
    }
}
