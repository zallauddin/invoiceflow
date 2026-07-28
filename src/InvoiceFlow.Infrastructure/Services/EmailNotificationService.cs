using System.Text;
using InvoiceFlow.Core.Entities;
using InvoiceFlow.Core.Enums;
using InvoiceFlow.Core.Interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace InvoiceFlow.Infrastructure.Services;

/// <summary>
/// Options for the email notification service.
/// </summary>
public class EmailNotificationOptions
{
    public const string SectionName = "EmailNotifications";

    public bool Enabled { get; set; } = false;
    public string SmtpHost { get; set; } = "localhost";
    public int SmtpPort { get; set; } = 587;
    public bool UseSsl { get; set; } = false;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromAddress { get; set; } = "noreply@invoiceflow.com";
    public string FromName { get; set; } = "InvoiceFlow";
}

/// <summary>
/// Result of sending an email notification.
/// </summary>
public record EmailNotificationResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Sends email notifications when invoice status changes.
/// Uses MailKit for SMTP delivery with HTML email templates.
/// </summary>
public interface IEmailNotificationService
{
    /// <summary>Sends a notification about an invoice status change.</summary>
    Task<EmailNotificationResult> SendInvoiceStatusNotificationAsync(
        Invoice invoice,
        InvoiceStatus oldStatus,
        InvoiceStatus newStatus,
        string recipientEmail,
        CancellationToken cancellationToken = default);

    /// <summary>Sends a compliance completion notification.</summary>
    Task<EmailNotificationResult> SendComplianceNotificationAsync(
        Invoice invoice,
        string recipientEmail,
        CancellationToken cancellationToken = default);
}

public sealed class EmailNotificationService : IEmailNotificationService
{
    private readonly EmailNotificationOptions _options;
    private readonly ILogger<EmailNotificationService> _logger;

    public EmailNotificationService(
        IOptions<EmailNotificationOptions> options,
        ILogger<EmailNotificationService> logger)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger;
    }

    public async Task<EmailNotificationResult> SendInvoiceStatusNotificationAsync(
        Invoice invoice,
        InvoiceStatus oldStatus,
        InvoiceStatus newStatus,
        string recipientEmail,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            _logger.LogDebug("Email notifications disabled — skipping status notification");
            return new EmailNotificationResult { Success = true };
        }

        try
        {
            var subject = $"[InvoiceFlow] Invoice {invoice.InvoiceNumber} — {FormatStatus(newStatus)}";
            var body = BuildStatusTemplate(invoice, oldStatus, newStatus);

            await SendEmailAsync(recipientEmail, subject, body, cancellationToken);

            _logger.LogInformation(
                "Sent status notification for invoice {InvoiceId} ({Old} → {New}) to {Email}",
                invoice.Id, oldStatus, newStatus, recipientEmail);

            return new EmailNotificationResult { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to send status notification for invoice {InvoiceId} to {Email}",
                invoice.Id, recipientEmail);

            return new EmailNotificationResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<EmailNotificationResult> SendComplianceNotificationAsync(
        Invoice invoice,
        string recipientEmail,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            _logger.LogDebug("Email notifications disabled — skipping compliance notification");
            return new EmailNotificationResult { Success = true };
        }

        try
        {
            var subject = $"[InvoiceFlow] Compliance Complete — Invoice {invoice.InvoiceNumber}";
            var body = BuildComplianceTemplate(invoice);

            await SendEmailAsync(recipientEmail, subject, body, cancellationToken);

            _logger.LogInformation(
                "Sent compliance notification for invoice {InvoiceId} to {Email}",
                invoice.Id, recipientEmail);

            return new EmailNotificationResult { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to send compliance notification for invoice {InvoiceId} to {Email}",
                invoice.Id, recipientEmail);

            return new EmailNotificationResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private async Task SendEmailAsync(
        string recipientEmail,
        string subject,
        string body,
        CancellationToken cancellationToken)
    {
        using var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));
        message.To.Add(new MailboxAddress("", recipientEmail));
        message.Subject = subject;

        var bodyBuilder = new BodyBuilder
        {
            HtmlBody = body
        };
        message.Body = bodyBuilder.ToMessageBody();

        using var client = new SmtpClient();
        client.ServerCertificateValidationCallback = (s, c, h, e) => true; // Accept all (dev)

        await client.ConnectAsync(_options.SmtpHost, _options.SmtpPort, _options.UseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls, cancellationToken);

        if (!string.IsNullOrEmpty(_options.Username))
        {
            await client.AuthenticateAsync(_options.Username, _options.Password, cancellationToken);
        }

        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }

    private static string BuildStatusTemplate(Invoice invoice, InvoiceStatus oldStatus, InvoiceStatus newStatus)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html><head><meta charset='utf-8'>");
        sb.AppendLine("<style>body{font-family:Arial,sans-serif;color:#333;max-width:600px;margin:0 auto;padding:20px}");
        sb.AppendLine(".header{background:#2563EB;color:white;padding:20px;border-radius:8px 8px 0 0}");
        sb.AppendLine(".content{padding:20px;border:1px solid #E2E8F0;border-top:none;border-radius:0 0 8px 8px}");
        sb.AppendLine(".status-badge{display:inline-block;padding:4px 12px;border-radius:12px;font-size:12px;font-weight:600}");
        sb.AppendLine(".status-changed{background:#FEF3C7;color:#92400E}");
        sb.AppendLine("table{width:100%;border-collapse:collapse;margin:16px 0}");
        sb.AppendLine("td{padding:8px 12px;border-bottom:1px solid #F1F5F9;font-size:14px}");
        sb.AppendLine("td:first-child{font-weight:600;color:#64748B;width:40%}");
        sb.AppendLine("</style></head><body>");
        sb.AppendLine("<div class='header'><h2 style='margin:0'>📄 Invoice Status Update</h2></div>");
        sb.AppendLine("<div class='content'>");
        sb.AppendLine($"<p>Invoice <strong>{invoice.InvoiceNumber}</strong> has changed status:</p>");
        sb.AppendLine("<table>");
        sb.AppendLine($"<tr><td>Previous Status</td><td><span class='status-badge'>{oldStatus}</span></td></tr>");
        sb.AppendLine($"<tr><td>Current Status</td><td><span class='status-badge status-changed'>{newStatus}</span></td></tr>");
        sb.AppendLine($"<tr><td>Vendor</td><td>{invoice.VendorName}</td></tr>");
        sb.AppendLine($"<tr><td>Total Amount</td><td>{invoice.TotalAmount:F2} {invoice.Currency}</td></tr>");
        sb.AppendLine($"<tr><td>Date</td><td>{invoice.InvoiceDate:yyyy-MM-dd}</td></tr>");
        sb.AppendLine("</table>");
        sb.AppendLine($"<p><a href='{invoice.Id}' style='display:inline-block;background:#2563EB;color:white;padding:10px 20px;border-radius:6px;text-decoration:none'>View Invoice Details →</a></p>");
        sb.AppendLine("</div></body></html>");
        return sb.ToString();
    }

    private static string BuildComplianceTemplate(Invoice invoice)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html><head><meta charset='utf-8'>");
        sb.AppendLine("<style>body{font-family:Arial,sans-serif;color:#333;max-width:600px;margin:0 auto;padding:20px}");
        sb.AppendLine(".header{background:#16A34A;color:white;padding:20px;border-radius:8px 8px 0 0}");
        sb.AppendLine(".content{padding:20px;border:1px solid #E2E8F0;border-top:none;border-radius:0 0 8px 8px}");
        sb.AppendLine("table{width:100%;border-collapse:collapse;margin:16px 0}");
        sb.AppendLine("td{padding:8px 12px;border-bottom:1px solid #F1F5F9;font-size:14px}");
        sb.AppendLine("td:first-child{font-weight:600;color:#64748B;width:40%}");
        sb.AppendLine("</style></head><body>");
        sb.AppendLine("<div class='header'><h2 style='margin:0'>🛡️ Compliance Complete</h2></div>");
        sb.AppendLine("<div class='content'>");
        sb.AppendLine($"<p>Invoice <strong>{invoice.InvoiceNumber}</strong> has passed compliance checks.</p>");
        sb.AppendLine("<table>");
        sb.AppendLine($"<tr><td>Compliance Model</td><td>{invoice.ComplianceModel}</td></tr>");
        sb.AppendLine($"<tr><td>Compliance ID</td><td>{invoice.ComplianceId ?? "—"}</td></tr>");
        sb.AppendLine($"<tr><td>Vendor</td><td>{invoice.VendorName}</td></tr>");
        sb.AppendLine($"<tr><td>Buyer</td><td>{invoice.BuyerName}</td></tr>");
        sb.AppendLine($"<tr><td>Total</td><td>{invoice.TotalAmount:F2} {invoice.Currency}</td></tr>");
        sb.AppendLine("</table>");
        sb.AppendLine($"<p><a href='{invoice.Id}' style='display:inline-block;background:#16A34A;color:white;padding:10px 20px;border-radius:6px;text-decoration:none'>View Details →</a></p>");
        sb.AppendLine("</div></body></html>");
        return sb.ToString();
    }

    private static string FormatStatus(InvoiceStatus status) => status switch
    {
        InvoiceStatus.Draft => "Draft",
        InvoiceStatus.Received => "Received",
        InvoiceStatus.Extracting => "Extracting Data",
        InvoiceStatus.Extracted => "Data Extracted",
        InvoiceStatus.PendingApproval => "Pending Approval",
        InvoiceStatus.Approved => "Approved",
        InvoiceStatus.Rejected => "Rejected",
        InvoiceStatus.Processing => "Processing Compliance",
        InvoiceStatus.Compliant => "Compliant",
        InvoiceStatus.NonCompliant => "Non-Compliant",
        InvoiceStatus.Transmitted => "Transmitted",
        InvoiceStatus.Failed => "Failed",
        InvoiceStatus.Cancelled => "Cancelled",
        _ => status.ToString()
    };
}
