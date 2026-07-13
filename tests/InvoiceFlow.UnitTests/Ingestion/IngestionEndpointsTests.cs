using System.Net;
using System.Security.Claims;
using FluentAssertions;
using InvoiceFlow.Api;
using InvoiceFlow.Core.Entities;
using InvoiceFlow.Core.Enums;
using InvoiceFlow.Core.Interfaces;
using InvoiceFlow.Core.Messages;
using MassTransit;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace InvoiceFlow.UnitTests.Ingestion;

/// <summary>
/// Tests for IngestionEndpoints — file upload endpoint validation, storage, and entity creation.
/// Uses WebApplicationFactory with mocked dependencies to verify Minimal API endpoint behavior.
/// </summary>
public class IngestionEndpointsTests : IClassFixture<IngestionEndpointsTests.IngestionWebApplicationFactory>
{
    private readonly IngestionWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly IStorageService _mockStorageService;
    private readonly IRepository<Document> _mockDocumentRepo;
    private readonly IRepository<Invoice> _mockInvoiceRepo;
    private readonly IPublishEndpoint _mockPublishEndpoint;

    public IngestionEndpointsTests(IngestionWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _mockStorageService = factory.MockStorageService;
        _mockDocumentRepo = factory.MockDocumentRepo;
        _mockInvoiceRepo = factory.MockInvoiceRepo;
        _mockPublishEndpoint = factory.MockPublishEndpoint;
    }

    // ─── POST /api/ingestion/upload — Valid File ─────────────────────

    [Fact]
    public async Task Upload_ValidPdfFile_ReturnsAccepted()
    {
        var content = CreateFileUpload("invoice.pdf", "application/pdf");

        var response = await _client.PostAsync("/api/ingestion/upload", content);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task Upload_ValidPdfFile_ReturnsInvoiceAndDocumentIds()
    {
        var content = CreateFileUpload("invoice.pdf", "application/pdf");

        var response = await _client.PostAsync("/api/ingestion/upload", content);
        var body = await response.Content.ReadAsStringAsync();

        body.Should().Contain("invoiceId");
        body.Should().Contain("documentId");
        body.Should().Contain("Accepted for processing");
    }

    [Fact]
    public async Task Upload_ValidPdfFile_UploadsToStorage()
    {
        var content = CreateFileUpload("invoice.pdf", "application/pdf");

        await _client.PostAsync("/api/ingestion/upload", content);

        await _mockStorageService.Received(1).UploadAsync(
            Arg.Is("documents"),
            Arg.Is<string>(p => p.Contains("/ingestion/")),
            Arg.Any<Stream>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Upload_ValidPdfFile_CreatesDocumentAndInvoice()
    {
        var content = CreateFileUpload("invoice.pdf", "application/pdf");

        await _client.PostAsync("/api/ingestion/upload", content);

        await _mockDocumentRepo.Received(1).AddAsync(
            Arg.Is<Document>(d => d.FileName == "invoice.pdf"),
            Arg.Any<CancellationToken>());

        await _mockInvoiceRepo.Received(1).AddAsync(
            Arg.Is<Invoice>(i =>
                i.OriginalFileName == "invoice.pdf" &&
                i.Source == IngestionSource.ApiUpload &&
                i.Status == InvoiceStatus.Received),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Upload_ValidPdfFile_PublishesExtractInvoiceCommand()
    {
        var content = CreateFileUpload("invoice.pdf", "application/pdf");

        await _client.PostAsync("/api/ingestion/upload", content);

        await _mockPublishEndpoint.Received(1).Publish(
            Arg.Is<ExtractInvoiceCommand>(c => c.FileName == "invoice.pdf"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Upload_ValidPdfFile_LinksDocumentToInvoice()
    {
        var content = CreateFileUpload("invoice.pdf", "application/pdf");

        await _client.PostAsync("/api/ingestion/upload", content);

        await _mockDocumentRepo.Received(1).UpdateAsync(
            Arg.Is<Document>(d => d.LinkedInvoiceId != null),
            Arg.Any<CancellationToken>());
    }

    // ─── POST /api/ingestion/upload — Invalid File Type ──────────────

    [Fact]
    public async Task Upload_ZipFile_ReturnsBadRequest()
    {
        var content = CreateFileUpload("archive.zip", "application/zip");

        var response = await _client.PostAsync("/api/ingestion/upload", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Upload_ZipFile_ReturnsErrorMessage()
    {
        var content = CreateFileUpload("archive.zip", "application/zip");

        var response = await _client.PostAsync("/api/ingestion/upload", content);
        var body = await response.Content.ReadAsStringAsync();

        body.Should().Contain(".zip");
        body.Should().Contain("not allowed");
    }

    [Theory]
    [InlineData("document.docx")]
    [InlineData("data.xlsx")]
    [InlineData("notes.txt")]
    [InlineData("script.exe")]
    public async Task Upload_UnsupportedExtension_ReturnsBadRequest(string fileName)
    {
        var content = CreateFileUpload(fileName, "application/octet-stream");

        var response = await _client.PostAsync("/api/ingestion/upload", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ─── POST /api/ingestion/upload — Oversized File ─────────────────

    [Fact]
    public async Task Upload_OversizedFile_ReturnsBadRequest()
    {
        var content = CreateOversizedFileUpload("huge.pdf");

        var response = await _client.PostAsync("/api/ingestion/upload", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Upload_OversizedFile_ReturnsSizeErrorMessage()
    {
        var content = CreateOversizedFileUpload("huge.pdf");

        var response = await _client.PostAsync("/api/ingestion/upload", content);
        var body = await response.Content.ReadAsStringAsync();

        body.Should().Contain("50MB");
    }

    // ─── POST /api/ingestion/upload — No File ────────────────────────

    [Fact]
    public async Task Upload_NoFile_ReturnsBadRequest()
    {
        var content = new MultipartFormDataContent();

        var response = await _client.PostAsync("/api/ingestion/upload", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Upload_NoFile_ReturnsUploadErrorMessage()
    {
        var content = new MultipartFormDataContent();

        var response = await _client.PostAsync("/api/ingestion/upload", content);
        var body = await response.Content.ReadAsStringAsync();

        body.Should().Contain("No file uploaded");
    }

    // ─── POST /api/ingestion/upload — Supported File Types ───────────

    [Theory]
    [InlineData("invoice.pdf", "application/pdf")]
    [InlineData("data.xml", "application/xml")]
    [InlineData("scan.jpg", "image/jpeg")]
    [InlineData("photo.png", "image/png")]
    [InlineData("document.tiff", "image/tiff")]
    [InlineData("scan.tif", "image/tiff")]
    public async Task Upload_SupportedFileType_ReturnsAccepted(string fileName, string contentType)
    {
        var content = CreateFileUpload(fileName, contentType);

        var response = await _client.PostAsync("/api/ingestion/upload", content);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }

    // ─── Helpers ─────────────────────────────────────────────────────

    private static MultipartFormDataContent CreateFileUpload(string fileName, string contentType)
    {
        var fileContent = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34 }; // %PDF-1.4
        var content = new MultipartFormDataContent();
        var fileContentPart = new ByteArrayContent(fileContent);
        fileContentPart.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        content.Add(fileContentPart, "file", fileName);
        return content;
    }

    private static MultipartFormDataContent CreateOversizedFileUpload(string fileName)
    {
        var oversizedContent = new byte[50 * 1024 * 1024 + 1]; // 50MB + 1 byte
        var content = new MultipartFormDataContent();
        var fileContentPart = new ByteArrayContent(oversizedContent);
        fileContentPart.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
        content.Add(fileContentPart, "file", fileName);
        return content;
    }

    // ─── Test Factory ────────────────────────────────────────────────

    public class IngestionWebApplicationFactory : WebApplicationFactory<Program>
    {
        public IStorageService MockStorageService { get; } = Substitute.For<IStorageService>();
        public IRepository<Document> MockDocumentRepo { get; } = Substitute.For<IRepository<Document>>();
        public IRepository<Invoice> MockInvoiceRepo { get; } = Substitute.For<IRepository<Invoice>>();
        public IPublishEndpoint MockPublishEndpoint { get; } = Substitute.For<IPublishEndpoint>();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                // Replace registered services with mocks
                services.AddSingleton(MockStorageService);
                services.AddSingleton(MockDocumentRepo);
                services.AddSingleton(MockInvoiceRepo);
                services.AddSingleton(MockPublishEndpoint);

                // Override authentication for testing
                services.AddAuthentication("Test")
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
                services.AddAuthorization(options =>
                {
                    options.DefaultPolicy = new AuthorizationPolicyBuilder("Test")
                        .RequireAuthenticatedUser()
                        .Build();
                });
            });
        }
    }
}

/// <summary>
/// Simplified authentication handler for integration tests that always succeeds.
/// </summary>
internal class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        Microsoft.Extensions.Logging.ILoggerFactory logger,
        System.Text.Encodings.Web.UrlEncoder encoder)
        : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, "TestUser"),
            new Claim("tenant_id", "00000000-0000-0000-0000-000000000001")
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "Test");
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
