using System.Net.Sockets;
using InvoiceFlow.Core.Entities;
using InvoiceFlow.Core.Enums;
using InvoiceFlow.Core.Interfaces;
using InvoiceFlow.Core.Messages;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;
using MassTransit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace InvoiceFlow.Infrastructure.Ingestion;

/// <summary>
/// IMAP-based email ingestion service that polls a mailbox for invoice attachments,
/// stores them in MinIO, creates Document entities, and publishes ExtractInvoiceCommand
/// for downstream extraction processing. Includes retry logic for transient IMAP failures.
/// </summary>
public sealed class EmailIngestionService : IEmailIngestionService, IAsyncDisposable
{
    private readonly EmailIngestionOptions _options;
    private readonly IStorageService _storageService;
    private readonly IRepository<Document> _documentRepository;
    private readonly IRepository<Invoice> _invoiceRepository;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<EmailIngestionService> _logger;
    private ImapClient? _imapClient;
    private bool _disposed;

    public EmailIngestionService(
        IOptions<EmailIngestionOptions> options,
        IStorageService storageService,
        IRepository<Document> documentRepository,
        IRepository<Invoice> invoiceRepository,
        IPublishEndpoint publishEndpoint,
        ILogger<EmailIngestionService> logger)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _storageService = storageService ?? throw new ArgumentNullException(nameof(storageService));
        _documentRepository = documentRepository ?? throw new ArgumentNullException(nameof(documentRepository));
        _invoiceRepository = invoiceRepository ?? throw new ArgumentNullException(nameof(invoiceRepository));
        _publishEndpoint = publishEndpoint ?? throw new ArgumentNullException(nameof(publishEndpoint));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<EmailIngestionResult> PollEmailsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Starting email poll from {Server}:{Port} folder {Folder}",
            _options.ImapServer, _options.ImapPort, _options.Folder);

        var processedCount = 0;
        var failedCount = 0;
        var createdDocuments = new List<Document>();

        try
        {
            await EnsureConnectedAsync(cancellationToken);

            var folder = _imapClient!.GetFolder(_options.Folder);
            await folder.OpenAsync(FolderAccess.ReadWrite, cancellationToken);

            // Search for unread messages
            var uids = folder.Search(
                SearchQuery.NotSeen,
                cancellationToken);

            _logger.LogInformation("Found {Count} unread emails in {Folder}", uids.Count, _options.Folder);

            foreach (var uid in uids)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var message = await folder.GetMessageAsync(uid, cancellationToken);
                    var attachmentResults = await ProcessMessageAttachmentsAsync(
                        message, cancellationToken);

                    if (attachmentResults.Count > 0)
                    {
                        createdDocuments.AddRange(attachmentResults);
                        processedCount += attachmentResults.Count;

                        // Mark email as read after successful processing
                        await folder.AddFlagsAsync(
                            uid,
                            MessageFlags.Seen,
                            true,
                            cancellationToken);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex,
                        "Failed to process email UID {Uid}: {ErrorMessage}",
                        uid, ex.Message);
                    failedCount++;
                }
            }

            _logger.LogInformation(
                "Email poll completed: {Processed} processed, {Failed} failed",
                processedCount, failedCount);

            return new EmailIngestionResult
            {
                Success = true,
                ProcessedCount = processedCount,
                FailedCount = failedCount,
                CreatedDocuments = createdDocuments
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Email poll failed: {ErrorMessage}", ex.Message);

            // Disconnect so next poll attempt reconnects
            await DisconnectGracefullyAsync();

            return new EmailIngestionResult
            {
                Success = false,
                ProcessedCount = processedCount,
                FailedCount = failedCount,
                ErrorMessage = ex.Message,
                CreatedDocuments = createdDocuments
            };
        }
    }

    /// <inheritdoc />
    public async Task<EmailIngestionResult> ProcessEmailAsync(
        string emailContent,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(emailContent);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        _logger.LogInformation("Processing single email content: {FileName}", fileName);

        var createdDocuments = new List<Document>();

        try
        {
            // Parse the raw email content using MimeKit
            using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(emailContent));
            var message = await MimeMessage.LoadAsync(stream, cancellationToken);

            var attachmentResults = await ProcessMessageAttachmentsAsync(
                message, cancellationToken);

            if (attachmentResults.Count > 0)
            {
                createdDocuments.AddRange(attachmentResults);

                return new EmailIngestionResult
                {
                    Success = true,
                    ProcessedCount = attachmentResults.Count,
                    FailedCount = 0,
                    CreatedDocuments = createdDocuments
                };
            }

            return new EmailIngestionResult
            {
                Success = true,
                ProcessedCount = 0,
                FailedCount = 0,
                ErrorMessage = "No matching attachments found in email",
                CreatedDocuments = createdDocuments
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to process email content {FileName}: {ErrorMessage}",
                fileName, ex.Message);

            return new EmailIngestionResult
            {
                Success = false,
                ProcessedCount = 0,
                FailedCount = 1,
                ErrorMessage = ex.Message,
                CreatedDocuments = createdDocuments
            };
        }
    }

    /// <summary>
    /// Extracts invoice-relevant attachments from a MIME message, uploads each to
    /// MinIO, creates Document and Invoice entities, and publishes ExtractInvoiceCommand.
    /// </summary>
    private async Task<List<Document>> ProcessMessageAttachmentsAsync(
        MimeMessage message,
        CancellationToken cancellationToken)
    {
        var documents = new List<Document>();
        var attachments = message.Attachments
            .OfType<MimePart>()
            .Where(IsAcceptedAttachment)
            .ToList();

        if (attachments.Count == 0)
        {
            _logger.LogDebug(
                "No accepted attachments in message {Subject} from {From}",
                message.Subject, message.From);
            return documents;
        }

        _logger.LogInformation(
            "Processing {Count} attachments from message {Subject}",
            attachments.Count, message.Subject);

        foreach (var attachment in attachments)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var document = await ProcessSingleAttachmentAsync(
                    attachment, message.Subject, cancellationToken);

                if (document is not null)
                {
                    documents.Add(document);

                    // Create Invoice entity for the document
                    var invoice = await CreateInvoiceForDocumentAsync(
                        document, cancellationToken);

                    // Publish extraction command
                    await PublishExtractionCommandAsync(
                        invoice, document, cancellationToken);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex,
                    "Failed to process attachment {FileName}: {ErrorMessage}",
                    attachment.FileName, ex.Message);
            }
        }

        return documents;
    }

    /// <summary>
    /// Uploads a single MIME attachment to MinIO and persists a Document entity.
    /// </summary>
    private async Task<Document?> ProcessSingleAttachmentAsync(
        MimePart attachment,
        string emailSubject,
        CancellationToken cancellationToken)
    {
        var fileName = attachment.FileName ?? $"attachment_{Guid.NewGuid():N}";
        var contentType = attachment.ContentType?.MimeType ?? "application/octet-stream";

        _logger.LogDebug(
            "Processing attachment: {FileName} ({ContentType})",
            fileName, contentType);

        // Stream the attachment content
        using var attachmentStream = new MemoryStream();
        await attachment.Content.DecodeToAsync(attachmentStream, cancellationToken);
        attachmentStream.Position = 0;

        // Build storage path using tenant-aware convention: {tenantId}/email-ingestion/{yyyy/MM}/{filename}
        var datePath = DateTime.UtcNow.ToString("yyyy/MM");
        var storageFileName = $"{Guid.NewGuid():N}_{SanitizeFileName(fileName)}";
        var objectName = $"{_options.TenantId}/email-ingestion/{datePath}/{storageFileName}";

        // Upload to MinIO
        await _storageService.UploadAsync(
            _options.BucketName,
            objectName,
            attachmentStream,
            contentType,
            cancellationToken);

        _logger.LogInformation(
            "Uploaded attachment {FileName} to {ObjectName}",
            fileName, objectName);

        // Create Document entity
        var document = new Document
        {
            Id = Guid.NewGuid(),
            TenantId = _options.TenantId,
            FileName = fileName,
            MimeType = contentType,
            FileSize = attachmentStream.Length,
            StoragePath = objectName,
            DocumentType = DocumentType.Invoice,
            Folder = "email-ingestion",
            Tags = $"[\"email\",\"{SanitizeFileName(emailSubject)}\"]"
        };

        var savedDocument = await _documentRepository.AddAsync(document, cancellationToken);

        _logger.LogInformation(
            "Created document {DocumentId} for attachment {FileName}",
            savedDocument.Id, fileName);

        return savedDocument;
    }

    /// <summary>
    /// Creates a Draft Invoice entity linked to the ingested document.
    /// </summary>
    private async Task<Invoice> CreateInvoiceForDocumentAsync(
        Document document,
        CancellationToken cancellationToken)
    {
        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            TenantId = _options.TenantId,
            DocumentType = DocumentType.Invoice,
            Status = InvoiceStatus.Draft,
            Source = IngestionSource.Email,
            OriginalFileName = document.FileName,
            StoragePath = document.StoragePath,
            MimeType = document.MimeType,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var savedInvoice = await _invoiceRepository.AddAsync(invoice, cancellationToken);

        // Link document to invoice
        document.LinkedInvoiceId = savedInvoice.Id;
        await _documentRepository.UpdateAsync(document, cancellationToken);

        _logger.LogInformation(
            "Created invoice {InvoiceId} linked to document {DocumentId}",
            savedInvoice.Id, document.Id);

        return savedInvoice;
    }

    /// <summary>
    /// Publishes an ExtractInvoiceCommand to the message bus for downstream extraction.
    /// </summary>
    private async Task PublishExtractionCommandAsync(
        Invoice invoice,
        Document document,
        CancellationToken cancellationToken)
    {
        var command = new ExtractInvoiceCommand
        {
            InvoiceId = invoice.Id,
            TenantId = _options.TenantId,
            DocumentId = document.Id,
            StoragePath = document.StoragePath,
            MimeType = document.MimeType,
            FileName = document.FileName,
            Priority = 0
        };

        await _publishEndpoint.Publish(command, cancellationToken);

        _logger.LogInformation(
            "Published ExtractInvoiceCommand for InvoiceId={InvoiceId}, DocumentId={DocumentId}",
            invoice.Id, document.Id);
    }

    /// <summary>
    /// Determines whether a MIME attachment is an accepted invoice file based on extension.
    /// </summary>
    private bool IsAcceptedAttachment(MimePart attachment)
    {
        var extension = Path.GetExtension(attachment.FileName ?? string.Empty);
        return !string.IsNullOrEmpty(extension)
            && _options.AttachmentExtensions.Contains(extension);
    }

    /// <summary>
    /// Ensures the IMAP client is connected and authenticated, reconnecting if necessary.
    /// Uses exponential backoff for transient connection failures.
    /// </summary>
    private async Task EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        if (_imapClient is { IsConnected: true })
        {
            return;
        }

        for (var attempt = 1; attempt <= _options.MaxRetryAttempts; attempt++)
        {
            try
            {
                _imapClient?.Dispose();
                _imapClient = new ImapClient();

                await _imapClient.ConnectAsync(
                    _options.ImapServer,
                    _options.ImapPort,
                    _options.UseSsl,
                    cancellationToken);

                await _imapClient.AuthenticateAsync(
                    _options.Username,
                    _options.Password,
                    cancellationToken);

                _logger.LogInformation(
                    "Connected to IMAP server {Server}:{Port} (attempt {Attempt})",
                    _options.ImapServer, _options.ImapPort, attempt);

                return;
            }
            catch (Exception ex) when (
                ex is IOException or SocketException or SslHandshakeException or
                    AuthenticationException or ImapProtocolException)
            {
                _logger.LogWarning(ex,
                    "IMAP connection attempt {Attempt}/{MaxAttempts} failed: {ErrorMessage}",
                    attempt, _options.MaxRetryAttempts, ex.Message);

                if (attempt == _options.MaxRetryAttempts)
                {
                    throw;
                }

                var delay = TimeSpan.FromSeconds(
                    _options.RetryBaseDelaySeconds * Math.Pow(2, attempt - 1));

                _logger.LogInformation(
                    "Retrying IMAP connection in {DelaySeconds}s",
                    delay.TotalSeconds);

                await Task.Delay(delay, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Gracefully disconnects the IMAP client, suppressing any errors during teardown.
    /// </summary>
    private async Task DisconnectGracefullyAsync()
    {
        if (_imapClient is null)
        {
            return;
        }

        try
        {
            if (_imapClient.IsConnected)
            {
                await _imapClient.DisconnectAsync(quit: true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error during IMAP disconnect (suppressed)");
        }
        finally
        {
            _imapClient.Dispose();
            _imapClient = null;
        }
    }

    /// <summary>
    /// Removes or replaces characters from a filename that are unsafe for storage paths.
    /// </summary>
    private static string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        return string.Concat(fileName.Select(c => invalidChars.Contains(c) ? '_' : c));
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await DisconnectGracefullyAsync();
    }
}
