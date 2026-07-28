using System.Globalization;
using System.Text;
using InvoiceFlow.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace InvoiceFlow.Api.Endpoints;

/// <summary>
/// Export endpoints — download invoice lists as CSV or Excel.
/// Uses CsvHelper for CSV generation and a simple tabular format for Excel.
/// </summary>
public static class ExportEndpoints
{
    public static WebApplication MapExportEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/export")
            .WithTags("Export")
            .RequireAuthorization("RequireViewer");

        // GET /api/export/invoices/csv — Export invoices as CSV
        group.MapGet("/invoices/csv", async (
            [FromServices] IRepository<InvoiceFlow.Core.Entities.Invoice> invoiceRepository,
            [FromServices] ITenantIdProvider tenantIdProvider,
            CancellationToken cancellationToken) =>
        {
            var tenantId = tenantIdProvider.TenantId;
            if (tenantId is null || tenantId.Value == Guid.Empty)
                return Results.BadRequest(new { error = "Tenant not resolved." });

            var invoices = await invoiceRepository.GetAllAsync(0, 5000, cancellationToken);

            var sb = new StringBuilder();
            sb.AppendLine("Invoice Number,Vendor Name,Buyer Name,Status,Currency,Subtotal,Tax Amount,Total Amount,Invoice Date,Due Date,Country,Compliance Model,Source,Created At");

            foreach (var inv in invoices)
            {
                sb.AppendLine(string.Join(",",
                    CsvEscape(inv.InvoiceNumber),
                    CsvEscape(inv.VendorName),
                    CsvEscape(inv.BuyerName),
                    inv.Status.ToString(),
                    inv.Currency,
                    inv.Subtotal.ToString("F2", CultureInfo.InvariantCulture),
                    inv.TaxAmount.ToString("F2", CultureInfo.InvariantCulture),
                    inv.TotalAmount.ToString("F2", CultureInfo.InvariantCulture),
                    inv.InvoiceDate.ToString("yyyy-MM-dd"),
                    inv.DueDate?.ToString("yyyy-MM-dd") ?? "",
                    inv.CountryCode ?? "",
                    inv.ComplianceModel?.ToString() ?? "",
                    inv.Source.ToString(),
                    inv.CreatedAt.ToString("O")
                ));
            }

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            return Results.File(bytes, "text/csv", $"invoices_{DateTime.UtcNow:yyyyMMdd}.csv");
        })
        .WithName("ExportInvoicesCsv")
        .WithSummary("Export invoice list as CSV")
        .Produces<byte[]>(200, "text/csv");

        // GET /api/export/invoices/excel — Export invoices as tab-separated (simple Excel)
        group.MapGet("/invoices/excel", async (
            [FromServices] IRepository<InvoiceFlow.Core.Entities.Invoice> invoiceRepository,
            [FromServices] ITenantIdProvider tenantIdProvider,
            CancellationToken cancellationToken) =>
        {
            var tenantId = tenantIdProvider.TenantId;
            if (tenantId is null || tenantId.Value == Guid.Empty)
                return Results.BadRequest(new { error = "Tenant not resolved." });

            var invoices = await invoiceRepository.GetAllAsync(0, 5000, cancellationToken);

            var sb = new StringBuilder();
            sb.AppendLine("Invoice Number\tVendor Name\tBuyer Name\tStatus\tCurrency\tSubtotal\tTax Amount\tTotal Amount\tInvoice Date\tDue Date\tCountry\tCompliance Model\tSource\tCreated At");

            foreach (var inv in invoices)
            {
                sb.AppendLine(string.Join("\t",
                    inv.InvoiceNumber,
                    inv.VendorName,
                    inv.BuyerName,
                    inv.Status.ToString(),
                    inv.Currency,
                    inv.Subtotal.ToString("F2", CultureInfo.InvariantCulture),
                    inv.TaxAmount.ToString("F2", CultureInfo.InvariantCulture),
                    inv.TotalAmount.ToString("F2", CultureInfo.InvariantCulture),
                    inv.InvoiceDate.ToString("yyyy-MM-dd"),
                    inv.DueDate?.ToString("yyyy-MM-dd") ?? "",
                    inv.CountryCode ?? "",
                    inv.ComplianceModel?.ToString() ?? "",
                    inv.Source.ToString(),
                    inv.CreatedAt.ToString("O")
                ));
            }

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            return Results.File(bytes, "text/tab-separated-values", $"invoices_{DateTime.UtcNow:yyyyMMdd}.tsv");
        })
        .WithName("ExportInvoicesExcel")
        .WithSummary("Export invoice list as tab-separated (Excel-compatible)")
        .Produces<byte[]>(200, "text/tab-separated-values");

        return app;
    }

    private static string CsvEscape(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "\"\"";
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return $"\"{value}\"";
    }
}
