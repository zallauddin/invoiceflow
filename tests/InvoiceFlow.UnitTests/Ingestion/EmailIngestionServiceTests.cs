using FluentAssertions;
using InvoiceFlow.Core.Entities;
using InvoiceFlow.Core.Enums;
using InvoiceFlow.Core.Interfaces;
using InvoiceFlow.Core.Messages;
using InvoiceFlow.Infrastructure.Ingestion;
using MassTransit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace InvoiceFlow.UnitTests.Ingestion;

/// <summary>
/// Tests for EmailIngestionService — IMAP email ingestion, attachment processing,
/// storage upload, entity creation, and extraction command publishing.
/// </summary>
public class EmailIngestionServiceTests : IAsyncLifetime
{
    private readonly IStorageService _mockStorageService = Substitute.For<IStorageService>();
    private readonly IRepository<Document> _mockDocumentRepo = Substitute.For<IRepository<Document>>();
    private readonly IRepository<Invoice> _mockInvoiceRepo = Substitute.For<IRepository<Invoice>>();
    private readonly IPublishEndpoint _mockPublishEndpoint = Substitute.For<IPublishEndpoint>();
    private readonly ILogger<EmailIngestionService> _mockLogger = Substitute.For<ILogger<EmailIngestionService>>();

    private readonly Guid _tenantId = Guid.NewGuid();

    private EmailIngestionService CreateSut(EmailIngestionOptions? options = null)
    {
        options ??= CreateDefaultOptions();
        return new EmailIngestionService(
            Options.Create(options),
            _mockStorageService,
            _mockDocumentRepo,
            _mockInvoiceRepo,
            _mockPublishEndpoint,
            _mockLogger);
    }

    private EmailIngestionOptions CreateDefaultOptions(Action<EmailIngestionOptions>? configure = null)
    {
        var options = new EmailIngestionOptions
        {
            ImapServer = "localhost",
            ImapPort = 993,
            Username = "test@example.com",
            Password = "password",
            UseSsl = true,
            Folder = "INBOX",
            TenantId = _tenantId,
            BucketName = "documents",
            MaxRetryAttempts = 1,
            RetryBaseDelaySeconds = 0,
            AttachmentExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".pdf", ".xml", ".jpg", ".png", ".tiff"
            }
        };

        configure?.Invoke(options);
        return options;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        // No resources to dispose in test fixtures
        await Task.CompletedTask;
    }

    private static string CreateMimeMessageWithAttachment(
        string fileName,
        string mimeType = "application/pdf",
        byte[]? content = null)
    {
        content ??= [0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34]; // %PDF-1.4 header

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("Sender", "sender@example.com"));
        message.To.Add(new MailboxAddress("Invoices", "invoices@company.com"));
        message.Subject = "Invoice Submission";

        var body = new TextPart("plain") { Text = "Please find attached invoice." };

        var attachment = new MimePart(mimeType.Split('/')[0], mimeType.Split('/')[1])
        {
            FileName = fileName,
            Content = new MimeContent(new MemoryStream(content))
        };

        var multipart = new Multipart("mixed") { body, attachment };
        message.Body = multipart;

        using var stream = new MemoryStream();
        message.WriteTo(stream);
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    private static string CreateMimeMessageWithoutAcceptedAttachments()
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("Sender", "sender@example.com"));
        message.To.Add(new MailboxAddress("Invoices", "invoices@company.com"));
        message.Subject = "No Invoice";

        var body = new TextPart("plain") { Text = "No attachments here." };

        var attachment = new MimePart("text", "plain")
        {
            FileName = "readme.txt",
            Content = new MimeContent(new MemoryStream(System.Text.Encoding.UTF8.GetBytes("Plain text content")))
        };

        var multipart = new Multipart("mixed") { body, attachment };
        message.Body = multipart;

        using var stream = new MemoryStream();
        message.WriteTo(stream);
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    // ─── Constructor Validation ──────────────────────────────────────

    [Fact]
    public void Constructor_NullOptions_ThrowsArgumentNullException()
    {
        var act = () => new EmailIngestionService(
            null!,
            _mockStorageService,
            _mockDocumentRepo,
            _mockInvoiceRepo,
            _mockPublishEndpoint,
            _mockLogger);

        act.Should().Throw<ArgumentNullException>()
            .Which.ParamName.Should().Be("options");
    }

    [Fact]
    public void Constructor_NullStorageService_ThrowsArgumentNullException()
    {
        var act = () => new EmailIngestionService(
            Options.Create(CreateDefaultOptions()),
            null!,
            _mockDocumentRepo,
            _mockInvoiceRepo,
            _mockPublishEndpoint,
            _mockLogger);

        act.Should().Throw<ArgumentNullException>()
            .Which.ParamName.Should().Be("storageService");
    }

    [Fact]
    public void Constructor_NullDocumentRepo_ThrowsArgumentNullException()
    {
        var act = () => new EmailIngestionService(
            Options.Create(CreateDefaultOptions()),
            _mockStorageService,
            null!,
            _mockInvoiceRepo,
            _mockPublishEndpoint,
            _mockLogger);

        act.Should().Throw<ArgumentNullException>()
            .Which.ParamName.Should().Be("documentRepository");
    }

    [Fact]
    public void Constructor_NullInvoiceRepo_ThrowsArgumentNullException()
    {
        var act = () => new EmailIngestionService(
            Options.Create(CreateDefaultOptions()),
            _mockStorageService,
            _mockDocumentRepo,
            null!,
            _mockPublishEndpoint,
            _mockLogger);

        act.Should().Throw<ArgumentNullException>()
            .Which.ParamName.Should().Be("invoiceRepository");
    }

    [Fact]
    public void Constructor_NullPublishEndpoint_ThrowsArgumentNullException()
    {
        var act = () => new EmailIngestionService(
            Options.Create(CreateDefaultOptions()),
            _mockStorageService,
            _mockDocumentRepo,
            _mockInvoiceRepo,
            null!,
            _mockLogger);

        act.Should().Throw<ArgumentNullException>()
            .Which.ParamName.Should().Be("publishEndpoint");
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        var act = () => new EmailIngestionService(
            Options.Create(CreateDefaultOptions()),
            _mockStorageService,
            _mockDocumentRepo,
            _mockInvoiceRepo,
            _mockPublishEndpoint,
            null!);

        act.Should().Throw<ArgumentNullException>()
            .Which.ParamName.Should().Be("logger");
    }

    // ─── PollEmailsAsync — Connection Failure ────────────────────────

    [Fact]
    public async Task PollEmailsAsync_ImapConnectionFails_ReturnsFailureResult()
    {
        // When there is no IMAP server available, EnsureConnectedAsync retries
        // and eventually throws. The outer catch returns a failure result.
        var options = CreateDefaultOptions(o =>
        {
            o.ImapServer = "127.0.0.1"; // No IMAP server running here
            o.ImapPort = 1993; // Non-standard port, unlikely to be listening
            o.MaxRetryAttempts = 1;
            o.RetryBaseDelaySeconds = 0;
        });

        var sut = CreateSut(options);

        var result = await sut.PollEmailsAsync(CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ProcessedCount.Should().Be(0);
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task PollEmailsAsync_ImapConnectionFails_DisconnectsGracefully()
    {
        var options = CreateDefaultOptions(o =>
        {
            o.ImapServer = "127.0.0.1";
            o.ImapPort = 1993;
            o.MaxRetryAttempts = 1;
            o.RetryBaseDelaySeconds = 0;
        });

        var sut = CreateSut(options);
        await sut.PollEmailsAsync(CancellationToken.None);

        // Should be able to call again (no stuck state)
        var result = await sut.PollEmailsAsync(CancellationToken.None);
        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task PollEmailsAsync_ImapConnectionFails_RetriesUpToMaxAttempts()
    {
        var options = CreateDefaultOptions(o =>
        {
            o.ImapServer = "127.0.0.1";
            o.ImapPort = 1993;
            o.MaxRetryAttempts = 2;
            o.RetryBaseDelaySeconds = 0;
        });

        var sut = CreateSut(options);

        var result = await sut.PollEmailsAsync(CancellationToken.None);

        result.Success.Should().BeFalse();
        result.CreatedDocuments.Should().BeEmpty();
    }

    [Fact]
    public async Task PollEmailsAsync_CancellationRequested_DoesNotHang()
    {
        var options = CreateDefaultOptions(o =>
        {
            o.ImapServer = "127.0.0.1";
            o.ImapPort = 1993;
            o.MaxRetryAttempts = 1;
            o.RetryBaseDelaySeconds = 10; // Long delay but should be cancelled
        });

        var sut = CreateSut(options);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        var result = await sut.PollEmailsAsync(cts.Token);

        // Should return failure due to cancellation, not hang
        result.Success.Should().BeFalse();
    }

    // ─── ProcessEmailAsync — Valid Email with Attachment ──────────────

    [Fact]
    public async Task ProcessEmailAsync_ValidPdfAttachment_UploadsToStorage()
    {
        var sut = CreateSut();
        var emailContent = CreateMimeMessageWithAttachment("invoice.pdf", "application/pdf");

        var savedDocument = new Document
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            FileName = "invoice.pdf",
            MimeType = "application/pdf",
            DocumentType = DocumentType.Invoice
        };

        _mockDocumentRepo.AddAsync(Arg.Any<Document>(), Arg.Any<CancellationToken>())
            .Returns(savedDocument);
        _mockInvoiceRepo.AddAsync(Arg.Any<Invoice>(), Arg.Any<CancellationToken>())
            .Returns(new Invoice { Id = Guid.NewGuid(), TenantId = _tenantId });

        var result = await sut.ProcessEmailAsync(emailContent, "test.eml");

        result.Success.Should().BeTrue();
        result.ProcessedCount.Should().Be(1);

        await _mockStorageService.Received(1).UploadAsync(
            Arg.Is("documents"),
            Arg.Is<string>(p => p.Contains(_tenantId.ToString()) && p.Contains("email-ingestion")),
            Arg.Any<Stream>(),
            Arg.Is("application/pdf"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessEmailAsync_ValidPdfAttachment_CreatesDocumentEntity()
    {
        var sut = CreateSut();
        var emailContent = CreateMimeMessageWithAttachment("invoice.pdf", "application/pdf");

        var savedDocument = new Document
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            FileName = "invoice.pdf"
        };

        _mockDocumentRepo.AddAsync(Arg.Any<Document>(), Arg.Any<CancellationToken>())
            .Returns(savedDocument);
        _mockInvoiceRepo.AddAsync(Arg.Any<Invoice>(), Arg.Any<CancellationToken>())
            .Returns(new Invoice { Id = Guid.NewGuid(), TenantId = _tenantId });

        var result = await sut.ProcessEmailAsync(emailContent, "test.eml");

        result.Success.Should().BeTrue();

        await _mockDocumentRepo.Received(1).AddAsync(
            Arg.Is<Document>(d =>
                d.FileName == "invoice.pdf" &&
                d.TenantId == _tenantId &&
                d.DocumentType == DocumentType.Invoice &&
                d.Folder == "email-ingestion"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessEmailAsync_ValidPdfAttachment_CreatesInvoiceEntity()
    {
        var sut = CreateSut();
        var emailContent = CreateMimeMessageWithAttachment("invoice.pdf", "application/pdf");

        var savedDocument = new Document
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            FileName = "invoice.pdf"
        };
        var savedInvoice = new Invoice
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId
        };

        _mockDocumentRepo.AddAsync(Arg.Any<Document>(), Arg.Any<CancellationToken>())
            .Returns(savedDocument);
        _mockInvoiceRepo.AddAsync(Arg.Any<Invoice>(), Arg.Any<CancellationToken>())
            .Returns(savedInvoice);

        var result = await sut.ProcessEmailAsync(emailContent, "test.eml");

        result.Success.Should().BeTrue();

        await _mockInvoiceRepo.Received(1).AddAsync(
            Arg.Is<Invoice>(i =>
                i.TenantId == _tenantId &&
                i.Status == InvoiceStatus.Draft &&
                i.Source == IngestionSource.Email &&
                i.OriginalFileName == "invoice.pdf"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessEmailAsync_ValidPdfAttachment_PublishesExtractionCommand()
    {
        var sut = CreateSut();
        var emailContent = CreateMimeMessageWithAttachment("invoice.pdf", "application/pdf");

        var documentId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();

        _mockDocumentRepo.AddAsync(Arg.Any<Document>(), Arg.Any<CancellationToken>())
            .Returns(new Document { Id = documentId, TenantId = _tenantId, FileName = "invoice.pdf" });
        _mockInvoiceRepo.AddAsync(Arg.Any<Invoice>(), Arg.Any<CancellationToken>())
            .Returns(new Invoice { Id = invoiceId, TenantId = _tenantId });

        var result = await sut.ProcessEmailAsync(emailContent, "test.eml");

        result.Success.Should().BeTrue();

        await _mockPublishEndpoint.Received(1).Publish(
            Arg.Is<ExtractInvoiceCommand>(c =>
                c.InvoiceId == invoiceId &&
                c.DocumentId == documentId &&
                c.TenantId == _tenantId &&
                c.FileName == "invoice.pdf"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessEmailAsync_ValidPdfAttachment_LinksDocumentToInvoice()
    {
        var sut = CreateSut();
        var emailContent = CreateMimeMessageWithAttachment("invoice.pdf", "application/pdf");

        var documentId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();

        var savedDocument = new Document { Id = documentId, TenantId = _tenantId };
        var savedInvoice = new Invoice { Id = invoiceId, TenantId = _tenantId };

        _mockDocumentRepo.AddAsync(Arg.Any<Document>(), Arg.Any<CancellationToken>())
            .Returns(savedDocument);
        _mockInvoiceRepo.AddAsync(Arg.Any<Invoice>(), Arg.Any<CancellationToken>())
            .Returns(savedInvoice);

        await sut.ProcessEmailAsync(emailContent, "test.eml");

        savedDocument.LinkedInvoiceId.Should().Be(invoiceId);

        await _mockDocumentRepo.Received(1).UpdateAsync(
            Arg.Is<Document>(d => d.LinkedInvoiceId == invoiceId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessEmailAsync_MultipleAcceptedAttachments_ProcessesAll()
    {
        var sut = CreateSut();

        // Create MIME message with two accepted attachments
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("Sender", "sender@example.com"));
        message.To.Add(new MailboxAddress("Invoices", "invoices@company.com"));
        message.Subject = "Multiple Invoices";

        var body = new TextPart("plain") { Text = "Multiple invoices attached." };
        var pdfAttachment = new MimePart("application", "pdf")
        {
            FileName = "invoice1.pdf",
            Content = new MimeContent(new MemoryStream([0x25, 0x50, 0x44, 0x46]))
        };
        var xmlAttachment = new MimePart("application", "xml")
        {
            FileName = "invoice2.xml",
            Content = new MimeContent(new MemoryStream(System.Text.Encoding.UTF8.GetBytes("<xml/>")))
        };

        message.Body = new Multipart("mixed") { body, pdfAttachment, xmlAttachment };

        using var stream = new MemoryStream();
        message.WriteTo(stream);
        var emailContent = System.Text.Encoding.UTF8.GetString(stream.ToArray());

        var callCount = 0;
        _mockDocumentRepo.AddAsync(Arg.Any<Document>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                callCount++;
                return new Document { Id = Guid.NewGuid(), TenantId = _tenantId, FileName = $"invoice{callCount}.pdf" };
            });
        _mockInvoiceRepo.AddAsync(Arg.Any<Invoice>(), Arg.Any<CancellationToken>())
            .Returns(new Invoice { Id = Guid.NewGuid(), TenantId = _tenantId });

        var result = await sut.ProcessEmailAsync(emailContent, "test.eml");

        result.Success.Should().BeTrue();
        result.ProcessedCount.Should().Be(2);
        result.CreatedDocuments.Should().HaveCount(2);
    }

    // ─── ProcessEmailAsync — No Matching Attachments ──────────────────

    [Fact]
    public async Task ProcessEmailAsync_NoAcceptedAttachments_ReturnsZeroProcessed()
    {
        var sut = CreateSut();
        var emailContent = CreateMimeMessageWithoutAcceptedAttachments();

        var result = await sut.ProcessEmailAsync(emailContent, "test.eml");

        result.Success.Should().BeTrue();
        result.ProcessedCount.Should().Be(0);
        result.FailedCount.Should().Be(0);
        result.ErrorMessage.Should().Be("No matching attachments found in email");
        result.CreatedDocuments.Should().BeEmpty();
    }

    [Fact]
    public async Task ProcessEmailAsync_NoAcceptedAttachments_DoesNotUploadToStorage()
    {
        var sut = CreateSut();
        var emailContent = CreateMimeMessageWithoutAcceptedAttachments();

        await sut.ProcessEmailAsync(emailContent, "test.eml");

        await _mockStorageService.DidNotReceive().UploadAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<Stream>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessEmailAsync_NoAcceptedAttachments_DoesNotCreateEntities()
    {
        var sut = CreateSut();
        var emailContent = CreateMimeMessageWithoutAcceptedAttachments();

        await sut.ProcessEmailAsync(emailContent, "test.eml");

        await _mockDocumentRepo.DidNotReceive().AddAsync(
            Arg.Any<Document>(),
            Arg.Any<CancellationToken>());
        await _mockInvoiceRepo.DidNotReceive().AddAsync(
            Arg.Any<Invoice>(),
            Arg.Any<CancellationToken>());
    }

    // ─── ProcessEmailAsync — Invalid Input ────────────────────────────

    [Fact]
    public async Task ProcessEmailAsync_InvalidMimeContent_ReturnsFailure()
    {
        var sut = CreateSut();

        var result = await sut.ProcessEmailAsync("not a valid mime message", "test.eml");

        result.Success.Should().BeFalse();
        result.FailedCount.Should().Be(1);
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ProcessEmailAsync_EmptyContent_ReturnsFailure()
    {
        var sut = CreateSut();

        var result = await sut.ProcessEmailAsync(string.Empty, "test.eml");

        result.Success.Should().BeFalse();
        result.FailedCount.Should().Be(1);
    }

    [Fact]
    public async Task ProcessEmailAsync_NullEmailContent_ThrowsArgumentException()
    {
        var sut = CreateSut();

        var act = async () => await sut.ProcessEmailAsync(null!, "test.eml");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ProcessEmailAsync_NullFileName_ThrowsArgumentException()
    {
        var sut = CreateSut();

        var act = async () => await sut.ProcessEmailAsync("content", null!);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    // ─── ProcessEmailAsync — Storage Failure ──────────────────────────

    [Fact]
    public async Task ProcessEmailAsync_StorageUploadFails_StillReturnsFailureForAttachment()
    {
        var sut = CreateSut();
        var emailContent = CreateMimeMessageWithAttachment("invoice.pdf");

        _mockStorageService.UploadAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<Stream>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Throws(new IOException("MinIO connection refused"));

        var result = await sut.ProcessEmailAsync(emailContent, "test.eml");

        // The attachment processing fails, but ProcessEmailAsync catches the exception
        // per-attachment and returns a failure result
        result.ProcessedCount.Should().Be(0);
    }

    // ─── ProcessEmailAsync — Supported Extensions ────────────────────

    [Theory]
    [InlineData("invoice.pdf", "application/pdf")]
    [InlineData("invoice.xml", "application/xml")]
    [InlineData("scan.jpg", "image/jpeg")]
    [InlineData("scan.png", "image/png")]
    [InlineData("scan.tiff", "image/tiff")]
    public async Task ProcessEmailAsync_SupportedExtension_ProcessesSuccessfully(
        string fileName,
        string mimeType)
    {
        var sut = CreateSut();
        var emailContent = CreateMimeMessageWithAttachment(fileName, mimeType);

        _mockDocumentRepo.AddAsync(Arg.Any<Document>(), Arg.Any<CancellationToken>())
            .Returns(new Document { Id = Guid.NewGuid(), TenantId = _tenantId, FileName = fileName });
        _mockInvoiceRepo.AddAsync(Arg.Any<Invoice>(), Arg.Any<CancellationToken>())
            .Returns(new Invoice { Id = Guid.NewGuid(), TenantId = _tenantId });

        var result = await sut.ProcessEmailAsync(emailContent, "test.eml");

        result.Success.Should().BeTrue();
        result.ProcessedCount.Should().Be(1);
    }

    [Theory]
    [InlineData("document.docx")]
    [InlineData("data.xlsx")]
    [InlineData("notes.txt")]
    [InlineData("archive.zip")]
    public async Task ProcessEmailAsync_UnsupportedExtension_ReturnsZeroProcessed(string fileName)
    {
        var sut = CreateSut();
        var emailContent = CreateMimeMessageWithAttachment(fileName, "application/octet-stream");

        var result = await sut.ProcessEmailAsync(emailContent, "test.eml");

        result.Success.Should().BeTrue();
        result.ProcessedCount.Should().Be(0);
        result.ErrorMessage.Should().Be("No matching attachments found in email");
    }
}
