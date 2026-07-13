using FluentAssertions;
using InvoiceFlow.Core.Entities;
using InvoiceFlow.Core.Enums;
using InvoiceFlow.Core.Events;
using InvoiceFlow.Core.Interfaces;
using InvoiceFlow.Infrastructure.AI.Extraction;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace InvoiceFlow.UnitTests.AI.Extraction;

/// <summary>
/// Tests for ExtractionOrchestrator — the multi-stage extraction pipeline
/// that tries OCR first, falls back to LLM, then template matching.
/// </summary>
public class ExtractionOrchestratorTests
{
    private readonly IOcrExtractionService _mockOcrService = Substitute.For<IOcrExtractionService>();
    private readonly ILlmExtractionService _mockLlmService = Substitute.For<ILlmExtractionService>();
    private readonly ITemplateMatchingService _mockTemplateService = Substitute.For<ITemplateMatchingService>();
    private readonly ILogger<ExtractionOrchestrator> _mockLogger = Substitute.For<ILogger<ExtractionOrchestrator>>();

    private ExtractionOrchestrator CreateSut()
        => new(_mockOcrService, _mockLlmService, _mockTemplateService, _mockLogger);

    private static (Document document, Invoice invoice) CreateTestEntities(
        Guid? documentId = null,
        Guid? invoiceId = null,
        Guid? tenantId = null)
    {
        var doc = new Document
        {
            Id = documentId ?? Guid.NewGuid(),
            TenantId = tenantId ?? Guid.NewGuid(),
            FileName = "invoice_001.pdf",
            MimeType = "image/png",
            StoragePath = "/storage/invoice_001.png"
        };

        var inv = new Invoice
        {
            Id = invoiceId ?? Guid.NewGuid(),
            TenantId = doc.TenantId,
            Status = InvoiceStatus.Received
        };

        return (doc, inv);
    }

    // ─── OCR Success Path (Confidence >= 0.85) ────────────────────────

    [Fact]
    public async Task ProcessAsync_OcrHighConfidence_CompletesWithoutLlmOrTemplate()
    {
        var (document, invoice) = CreateTestEntities();
        var sut = CreateSut();

        _mockOcrService.ExtractFromImageAsync(
                document.StoragePath,
                Arg.Any<OcrExtractionOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(new OcrExtractionResult
            {
                Fields = new Dictionary<string, string>
                {
                    ["InvoiceNumber"] = "INV-001",
                    ["VendorName"] = "Acme Corp",
                    ["TotalAmount"] = "1190.00",
                    ["InvoiceDate"] = "15.01.2024"
                },
                Confidence = 0.92,
                FieldConfidences = new Dictionary<string, double>
                {
                    ["InvoiceNumber"] = 0.95,
                    ["VendorName"] = 0.88,
                    ["TotalAmount"] = 0.93
                },
                RawText = "Invoice No: INV-001\nVendor: Acme Corp\nTotal: 1190.00"
            });

        var result = await sut.ProcessAsync(document, invoice);

        result.Success.Should().BeTrue();
        result.ExtractionMethod.Should().Be(ExtractionMethod.Ocr);
        result.Confidence.Should().Be(0.92);
        result.Invoice.InvoiceNumber.Should().Be("INV-001");
        result.Invoice.VendorName.Should().Be("Acme Corp");

        // LLM and template should NOT be called when OCR confidence is high
        await _mockLlmService.DidNotReceive().ExtractFromTextAsync(
            Arg.Any<string>(),
            Arg.Any<LlmExtractionOptions?>(),
            Arg.Any<CancellationToken>());
        await _mockTemplateService.DidNotReceive().MatchOrCreateTemplateAsync(
            Arg.Any<string>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessAsync_OcrHighConfidence_SetsInvoiceStatusToExtracted()
    {
        var (document, invoice) = CreateTestEntities();
        var sut = CreateSut();

        _mockOcrService.ExtractFromImageAsync(
                Arg.Any<string>(),
                Arg.Any<OcrExtractionOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(new OcrExtractionResult
            {
                Fields = new Dictionary<string, string> { ["InvoiceNumber"] = "INV-001" },
                Confidence = 0.90,
                FieldConfidences = new Dictionary<string, double> { ["InvoiceNumber"] = 0.90 },
                RawText = "Invoice No: INV-001"
            });

        var result = await sut.ProcessAsync(document, invoice);

        invoice.Status.Should().Be(InvoiceStatus.Extracted);
        invoice.ExtractedAt.Should().NotBeNull();
        invoice.ExtractionMethod.Should().Be(ExtractionMethod.Ocr);
        invoice.OcrConfidence.Should().Be(0.90);
    }

    // ─── LLM Fallback When OCR Confidence < 0.85 ─────────────────────

    [Fact]
    public async Task ProcessAsync_OcrLowConfidence_FallsBackToLlm()
    {
        var (document, invoice) = CreateTestEntities();
        var sut = CreateSut();

        _mockOcrService.ExtractFromImageAsync(
                Arg.Any<string>(),
                Arg.Any<OcrExtractionOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(new OcrExtractionResult
            {
                Fields = new Dictionary<string, string>
                {
                    ["InvoiceNumber"] = "INV-001",
                    ["TotalAmount"] = "500.00"
                },
                Confidence = 0.60,
                FieldConfidences = new Dictionary<string, double> { ["InvoiceNumber"] = 0.60 },
                RawText = "Invoice No INV-001 Total 500.00"
            });

        _mockLlmService.ExtractFromTextAsync(
                Arg.Any<string>(),
                Arg.Any<LlmExtractionOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(new LlmExtractionResult
            {
                Fields = new Dictionary<string, string>
                {
                    ["InvoiceNumber"] = "INV-001",
                    ["VendorName"] = "Acme Corp",
                    ["TotalAmount"] = "500.00",
                    ["InvoiceDate"] = "2024-01-15"
                },
                Confidence = 0.91,
                FieldConfidences = new Dictionary<string, double>
                {
                    ["InvoiceNumber"] = 0.95,
                    ["VendorName"] = 0.88,
                    ["TotalAmount"] = 0.90
                },
                Provider = LlmProvider.Anthropic,
                Model = "claude-3-5-sonnet-20241022"
            });

        var result = await sut.ProcessAsync(document, invoice);

        result.Success.Should().BeTrue();
        result.ExtractionMethod.Should().Be(ExtractionMethod.Llm);
        result.Confidence.Should().Be(0.91);
        result.Invoice.VendorName.Should().Be("Acme Corp");

        await _mockLlmService.Received(1).ExtractFromTextAsync(
            Arg.Any<string>(),
            Arg.Any<LlmExtractionOptions?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessAsync_OcrLowConfidenceLlmWorse_KeepsOcrResult()
    {
        var (document, invoice) = CreateTestEntities();
        var sut = CreateSut();

        _mockOcrService.ExtractFromImageAsync(
                Arg.Any<string>(),
                Arg.Any<OcrExtractionOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(new OcrExtractionResult
            {
                Fields = new Dictionary<string, string> { ["InvoiceNumber"] = "INV-001" },
                Confidence = 0.70,
                FieldConfidences = new Dictionary<string, double> { ["InvoiceNumber"] = 0.70 },
                RawText = "Invoice No: INV-001"
            });

        _mockLlmService.ExtractFromTextAsync(
                Arg.Any<string>(),
                Arg.Any<LlmExtractionOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(new LlmExtractionResult
            {
                Fields = new Dictionary<string, string> { ["InvoiceNumber"] = "INV-002" },
                Confidence = 0.50,
                FieldConfidences = new Dictionary<string, double>(),
                Provider = LlmProvider.OpenAI
            });

        var result = await sut.ProcessAsync(document, invoice);

        // LLM confidence (0.50) is lower than OCR (0.70), so OCR result is kept
        result.ExtractionMethod.Should().Be(ExtractionMethod.Ocr);
        result.Confidence.Should().Be(0.70);
    }

    [Fact]
    public async Task ProcessAsync_OcrLowConfidence_LlmNotCalledWhenRawTextEmpty()
    {
        var (document, invoice) = CreateTestEntities();
        var sut = CreateSut();

        _mockOcrService.ExtractFromImageAsync(
                Arg.Any<string>(),
                Arg.Any<OcrExtractionOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(new OcrExtractionResult
            {
                Fields = new Dictionary<string, string>(),
                Confidence = 0.30,
                FieldConfidences = new Dictionary<string, double>(),
                RawText = "" // empty raw text
            });

        var result = await sut.ProcessAsync(document, invoice);

        await _mockLlmService.DidNotReceive().ExtractFromTextAsync(
            Arg.Any<string>(),
            Arg.Any<LlmExtractionOptions?>(),
            Arg.Any<CancellationToken>());
    }

    // ─── Template Fallback When Both OCR and LLM Fail ─────────────────

    [Fact]
    public async Task ProcessAsync_BothOcrAndLlmLow_FallsBackToTemplate()
    {
        var (document, invoice) = CreateTestEntities();
        var sut = CreateSut();

        _mockOcrService.ExtractFromImageAsync(
                Arg.Any<string>(),
                Arg.Any<OcrExtractionOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(new OcrExtractionResult
            {
                Fields = new Dictionary<string, string>(),
                Confidence = 0.30,
                FieldConfidences = new Dictionary<string, double>(),
                RawText = "Garbled text from OCR"
            });

        _mockLlmService.ExtractFromTextAsync(
                Arg.Any<string>(),
                Arg.Any<LlmExtractionOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(new LlmExtractionResult
            {
                Fields = new Dictionary<string, string>(),
                Confidence = 0.40,
                FieldConfidences = new Dictionary<string, double>(),
                Provider = LlmProvider.Anthropic
            });

        _mockTemplateService.MatchOrCreateTemplateAsync(
                Arg.Any<string>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(new TemplateMatchResult
            {
                HasMatch = true,
                Confidence = 0.88,
                ExtractedFields = new Dictionary<string, string>
                {
                    ["InvoiceNumber"] = "TPL-001",
                    ["VendorName"] = "Template Vendor",
                    ["TotalAmount"] = "750.00"
                },
                FieldConfidences = new Dictionary<string, double>
                {
                    ["InvoiceNumber"] = 0.90,
                    ["VendorName"] = 0.85,
                    ["TotalAmount"] = 0.88
                }
            });

        var result = await sut.ProcessAsync(document, invoice);

        result.Success.Should().BeTrue();
        result.ExtractionMethod.Should().Be(ExtractionMethod.TemplateAi);
        result.Confidence.Should().Be(0.88);
        result.Invoice.InvoiceNumber.Should().Be("TPL-001");
        result.Invoice.VendorName.Should().Be("Template Vendor");
    }

    [Fact]
    public async Task ProcessAsync_AllStagesFail_ReturnsFailure()
    {
        var (document, invoice) = CreateTestEntities();
        var sut = CreateSut();

        _mockOcrService.ExtractFromImageAsync(
                Arg.Any<string>(),
                Arg.Any<OcrExtractionOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(new OcrExtractionResult
            {
                Fields = new Dictionary<string, string>(),
                Confidence = 0,
                RawText = ""
            });

        _mockLlmService.ExtractFromTextAsync(
                Arg.Any<string>(),
                Arg.Any<LlmExtractionOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(new LlmExtractionResult
            {
                Fields = new Dictionary<string, string>(),
                Confidence = 0
            });

        _mockTemplateService.MatchOrCreateTemplateAsync(
                Arg.Any<string>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(new TemplateMatchResult
            {
                HasMatch = false,
                Confidence = 0
            });

        var result = await sut.ProcessAsync(document, invoice);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("No fields could be extracted");
    }

    // ─── OCR Exception Handling ───────────────────────────────────────

    [Fact]
    public async Task ProcessAsync_OcrThrowsException_FallsToLlm()
    {
        var (document, invoice) = CreateTestEntities();
        var sut = CreateSut();

        _mockOcrService.ExtractFromImageAsync(
                Arg.Any<string>(),
                Arg.Any<OcrExtractionOptions?>(),
                Arg.Any<CancellationToken>())
            .Throws(new IOException("Tesseract engine failed"));

        _mockLlmService.ExtractFromTextAsync(
                Arg.Any<string>(),
                Arg.Any<LlmExtractionOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(new LlmExtractionResult
            {
                Fields = new Dictionary<string, string>
                {
                    ["InvoiceNumber"] = "INV-FALLBACK"
                },
                Confidence = 0.88,
                FieldConfidences = new Dictionary<string, double> { ["InvoiceNumber"] = 0.88 },
                Provider = LlmProvider.Anthropic
            });

        var result = await sut.ProcessAsync(document, invoice);

        result.Success.Should().BeTrue();
        result.ExtractionMethod.Should().Be(ExtractionMethod.Llm);
    }

    // ─── InvoiceLines Creation ────────────────────────────────────────

    [Fact]
    public async Task ProcessAsync_WithJsonLineItems_CreatesInvoiceLines()
    {
        var (document, invoice) = CreateTestEntities();
        var sut = CreateSut();

        var lineItemsJson = """
        [
            {"Description": "Widget A", "Quantity": "10", "UnitPrice": "25.00", "TotalPrice": "250.00", "TaxRate": "19"},
            {"Description": "Widget B", "Quantity": "5", "UnitPrice": "50.00", "TotalPrice": "250.00", "TaxRate": "19"}
        ]
        """;

        _mockOcrService.ExtractFromImageAsync(
                Arg.Any<string>(),
                Arg.Any<OcrExtractionOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(new OcrExtractionResult
            {
                Fields = new Dictionary<string, string>
                {
                    ["InvoiceNumber"] = "INV-LINES",
                    ["LineItems"] = lineItemsJson,
                    ["TotalAmount"] = "600.00"
                },
                Confidence = 0.92,
                FieldConfidences = new Dictionary<string, double> { ["InvoiceNumber"] = 0.92 },
                RawText = "Line items test"
            });

        var result = await sut.ProcessAsync(document, invoice);

        result.Success.Should().BeTrue();
        invoice.Lines.Should().HaveCount(2);
        invoice.Lines[0].Description.Should().Be("Widget A");
        invoice.Lines[0].Quantity.Should().Be(10m);
        invoice.Lines[0].UnitPrice.Should().Be(25.00m);
        invoice.Lines[0].LineTotal.Should().Be(250.00m);
        invoice.Lines[1].Description.Should().Be("Widget B");
        invoice.Lines[1].Quantity.Should().Be(5m);
        invoice.Lines[0].InvoiceId.Should().Be(invoice.Id);
    }

    [Fact]
    public async Task ProcessAsync_WithSingleDescriptionField_CreatesSingleLine()
    {
        var (document, invoice) = CreateTestEntities();
        var sut = CreateSut();

        _mockOcrService.ExtractFromImageAsync(
                Arg.Any<string>(),
                Arg.Any<OcrExtractionOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(new OcrExtractionResult
            {
                Fields = new Dictionary<string, string>
                {
                    ["InvoiceNumber"] = "INV-SINGLE",
                    ["Description"] = "Consulting Services",
                    ["Quantity"] = "1",
                    ["UnitPrice"] = "500.00",
                    ["LineTotal"] = "500.00",
                    ["TotalAmount"] = "500.00"
                },
                Confidence = 0.90,
                FieldConfidences = new Dictionary<string, double> { ["InvoiceNumber"] = 0.90 },
                RawText = "Single line item"
            });

        var result = await sut.ProcessAsync(document, invoice);

        invoice.Lines.Should().HaveCount(1);
        invoice.Lines[0].Description.Should().Be("Consulting Services");
        invoice.Lines[0].Quantity.Should().Be(1m);
    }

    // ─── Totals Calculation ───────────────────────────────────────────

    [Fact]
    public async Task ProcessAsync_WithLineItems_CalculatesTotalsFromLines()
    {
        var (document, invoice) = CreateTestEntities();
        var sut = CreateSut();

        var lineItemsJson = """
        [
            {"Description": "Item A", "Quantity": "10", "UnitPrice": "100.00", "TotalPrice": "1000.00", "TaxRate": "10"},
            {"Description": "Item B", "Quantity": "5", "UnitPrice": "200.00", "TotalPrice": "1000.00", "TaxRate": "10"}
        ]
        """;

        _mockOcrService.ExtractFromImageAsync(
                Arg.Any<string>(),
                Arg.Any<OcrExtractionOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(new OcrExtractionResult
            {
                Fields = new Dictionary<string, string>
                {
                    ["InvoiceNumber"] = "INV-TOTALS",
                    ["LineItems"] = lineItemsJson
                },
                Confidence = 0.90,
                FieldConfidences = new Dictionary<string, double> { ["InvoiceNumber"] = 0.90 },
                RawText = "Totals test"
            });

        var result = await sut.ProcessAsync(document, invoice);

        invoice.Lines.Should().HaveCount(2);
        invoice.Lines[0].TaxAmount.Should().Be(100.00m); // 1000.00 * 10 / 100
        invoice.Lines[1].TaxAmount.Should().Be(100.00m); // 1000.00 * 10 / 100

        // Subtotal should be computed from lines (2000.00)
        invoice.Subtotal.Should().Be(2000.00m);
        invoice.TaxAmount.Should().Be(200.00m);
        invoice.TotalAmount.Should().Be(2200.00m); // Subtotal + Tax
    }

    [Fact]
    public async Task ProcessAsync_ExtractedAmountsNotZero_NotOverriddenByLines()
    {
        var (document, invoice) = CreateTestEntities();
        var sut = CreateSut();

        var lineItemsJson = """
        [
            {"Description": "Item A", "Quantity": "1", "UnitPrice": "100.00", "TotalPrice": "100.00"}
        ]
        """;

        _mockOcrService.ExtractFromImageAsync(
                Arg.Any<string>(),
                Arg.Any<OcrExtractionOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(new OcrExtractionResult
            {
                Fields = new Dictionary<string, string>
                {
                    ["InvoiceNumber"] = "INV-PRESERVE",
                    ["LineItems"] = lineItemsJson,
                    ["SubtotalAmount"] = "1500.00", // Already set
                    ["TaxAmount"] = "250.00" // Already set
                },
                Confidence = 0.90,
                FieldConfidences = new Dictionary<string, double> { ["InvoiceNumber"] = 0.90 },
                RawText = "Preserve amounts"
            });

        var result = await sut.ProcessAsync(document, invoice);

        // Subtotal and TaxAmount were already set (non-zero), so they should NOT be overridden
        invoice.Subtotal.Should().Be(1500.00m);
        invoice.TaxAmount.Should().Be(250.00m);
        // TotalAmount is always Subtotal + TaxAmount
        invoice.TotalAmount.Should().Be(1750.00m);
    }

    // ─── Domain Events ────────────────────────────────────────────────

    [Fact]
    public async Task ProcessAsync_SuccessfulExtraction_RaisesInvoiceExtractedEvent()
    {
        var (document, invoice) = CreateTestEntities();
        var sut = CreateSut();

        _mockOcrService.ExtractFromImageAsync(
                Arg.Any<string>(),
                Arg.Any<OcrExtractionOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(new OcrExtractionResult
            {
                Fields = new Dictionary<string, string> { ["InvoiceNumber"] = "INV-001" },
                Confidence = 0.90,
                FieldConfidences = new Dictionary<string, double> { ["InvoiceNumber"] = 0.90 },
                RawText = "Invoice No: INV-001"
            });

        var result = await sut.ProcessAsync(document, invoice);

        result.DomainEvents.Should().HaveCount(1);
        var domainEvent = result.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<InvoiceExtractedEvent>().Which;

        domainEvent.InvoiceId.Should().Be(invoice.Id);
        domainEvent.TenantId.Should().Be(invoice.TenantId);
        domainEvent.ExtractionMethod.Should().Be("Ocr");
        domainEvent.Confidence.Should().Be(0.90);
    }

    [Fact]
    public async Task ProcessAsync_SuccessfulExtraction_LlmMethod_RaisesEventWithLlmMethod()
    {
        var (document, invoice) = CreateTestEntities();
        var sut = CreateSut();

        _mockOcrService.ExtractFromImageAsync(
                Arg.Any<string>(),
                Arg.Any<OcrExtractionOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(new OcrExtractionResult
            {
                Fields = new Dictionary<string, string>(),
                Confidence = 0.50,
                FieldConfidences = new Dictionary<string, double>(),
                RawText = "Some OCR text"
            });

        _mockLlmService.ExtractFromTextAsync(
                Arg.Any<string>(),
                Arg.Any<LlmExtractionOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(new LlmExtractionResult
            {
                Fields = new Dictionary<string, string> { ["InvoiceNumber"] = "INV-LLM" },
                Confidence = 0.92,
                FieldConfidences = new Dictionary<string, double> { ["InvoiceNumber"] = 0.92 },
                Provider = LlmProvider.OpenAI
            });

        var result = await sut.ProcessAsync(document, invoice);

        var domainEvent = result.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<InvoiceExtractedEvent>().Which;
        domainEvent.ExtractionMethod.Should().Be("Llm");
    }

    // ─── Document Storage Path ────────────────────────────────────────

    [Fact]
    public async Task ProcessAsync_OcrSetsRawTextOnDocument()
    {
        var (document, invoice) = CreateTestEntities();
        var sut = CreateSut();

        _mockOcrService.ExtractFromImageAsync(
                Arg.Any<string>(),
                Arg.Any<OcrExtractionOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(new OcrExtractionResult
            {
                Fields = new Dictionary<string, string> { ["InvoiceNumber"] = "INV-001" },
                Confidence = 0.90,
                FieldConfidences = new Dictionary<string, double> { ["InvoiceNumber"] = 0.90 },
                RawText = "Extracted OCR text content"
            });

        await sut.ProcessAsync(document, invoice);

        document.OcrText.Should().Be("Extracted OCR text content");
    }

    [Fact]
    public async Task ProcessAsync_NoStoragePath_SkipsOcr()
    {
        var (document, invoice) = CreateTestEntities();
        document.StoragePath = ""; // No storage path

        var sut = CreateSut();

        _mockLlmService.ExtractFromTextAsync(
                Arg.Any<string>(),
                Arg.Any<LlmExtractionOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(new LlmExtractionResult
            {
                Fields = new Dictionary<string, string> { ["InvoiceNumber"] = "INV-NOFILE" },
                Confidence = 0.88,
                FieldConfidences = new Dictionary<string, double> { ["InvoiceNumber"] = 0.88 },
                Provider = LlmProvider.Anthropic
            });

        _mockTemplateService.MatchOrCreateTemplateAsync(
                Arg.Any<string>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(new TemplateMatchResult
            {
                HasMatch = true,
                Confidence = 0.90,
                ExtractedFields = new Dictionary<string, string>
                {
                    ["InvoiceNumber"] = "TPL-NOFILE"
                },
                FieldConfidences = new Dictionary<string, double>
                {
                    ["InvoiceNumber"] = 0.90
                }
            });

        var result = await sut.ProcessAsync(document, invoice);

        await _mockOcrService.DidNotReceive().ExtractFromImageAsync(
            Arg.Any<string>(),
            Arg.Any<OcrExtractionOptions?>(),
            Arg.Any<CancellationToken>());
    }

    // ─── PDF Document Detection ───────────────────────────────────────

    [Fact]
    public async Task ProcessAsync_PdfMimeType_CallsExtractFromPdf()
    {
        var (document, invoice) = CreateTestEntities();
        document.MimeType = "application/pdf";
        document.StoragePath = "/storage/invoice.pdf";

        var sut = CreateSut();

        _mockOcrService.ExtractFromPdfAsync(
                document.StoragePath,
                Arg.Any<OcrExtractionOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(new OcrExtractionResult
            {
                Fields = new Dictionary<string, string> { ["InvoiceNumber"] = "INV-PDF" },
                Confidence = 0.90,
                FieldConfidences = new Dictionary<string, double> { ["InvoiceNumber"] = 0.90 },
                RawText = "PDF content"
            });

        var result = await sut.ProcessAsync(document, invoice);

        await _mockOcrService.Received(1).ExtractFromPdfAsync(
            document.StoragePath,
            Arg.Any<OcrExtractionOptions?>(),
            Arg.Any<CancellationToken>());
        await _mockOcrService.DidNotReceive().ExtractFromImageAsync(
            Arg.Any<string>(),
            Arg.Any<OcrExtractionOptions?>(),
            Arg.Any<CancellationToken>());
    }

    // ─── Field Mapping to Invoice ─────────────────────────────────────

    [Fact]
    public async Task ProcessAsync_MapsAllExtractedFieldsToInvoice()
    {
        var (document, invoice) = CreateTestEntities();
        var sut = CreateSut();

        _mockOcrService.ExtractFromImageAsync(
                Arg.Any<string>(),
                Arg.Any<OcrExtractionOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(new OcrExtractionResult
            {
                Fields = new Dictionary<string, string>
                {
                    ["InvoiceNumber"] = "INV-FULL",
                    ["InvoiceDate"] = "15.01.2024",
                    ["DueDate"] = "15.02.2024",
                    ["VendorName"] = "Full Vendor",
                    ["VendorTaxId"] = "NL123456789B01",
                    ["CustomerName"] = "Full Customer",
                    ["CustomerTaxId"] = "NL987654321B02",
                    ["Currency"] = "USD",
                    ["TotalAmount"] = "2500.00",
                    ["TaxAmount"] = "475.00",
                    ["SubtotalAmount"] = "2025.00"
                },
                Confidence = 0.95,
                FieldConfidences = new Dictionary<string, double> { ["InvoiceNumber"] = 0.95 },
                RawText = "Full extraction"
            });

        var result = await sut.ProcessAsync(document, invoice);

        invoice.InvoiceNumber.Should().Be("INV-FULL");
        invoice.InvoiceDate.Should().Be(new DateTime(2024, 1, 15));
        invoice.DueDate.Should().Be(new DateTime(2024, 2, 15));
        invoice.VendorName.Should().Be("Full Vendor");
        invoice.VendorTaxId.Should().Be("NL123456789B01");
        invoice.BuyerName.Should().Be("Full Customer");
        invoice.BuyerTaxId.Should().Be("NL987654321B02");
        invoice.Currency.Should().Be("USD");
        invoice.TotalAmount.Should().Be(2500.00m);
        invoice.TaxAmount.Should().Be(475.00m);
        invoice.Subtotal.Should().Be(2025.00m);
    }

    // ─── Amount Parsing Edge Cases ────────────────────────────────────

    [Theory]
    [InlineData("€1.190,00", 1190.00)] // European format
    [InlineData("$1,190.00", 1190.00)] // US format
    [InlineData("EUR 1190.00", 1190.00)] // Currency prefix
    [InlineData("1190.00", 1190.00)] // Plain number
    [InlineData("1.190,00", 1190.00)] // European without symbol
    public async Task ProcessAsync_ParsesVariousAmountFormats(string amountString, decimal expected)
    {
        var (document, invoice) = CreateTestEntities();
        var sut = CreateSut();

        _mockOcrService.ExtractFromImageAsync(
                Arg.Any<string>(),
                Arg.Any<OcrExtractionOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(new OcrExtractionResult
            {
                Fields = new Dictionary<string, string>
                {
                    ["InvoiceNumber"] = "INV-AMT",
                    ["TotalAmount"] = amountString
                },
                Confidence = 0.90,
                FieldConfidences = new Dictionary<string, double> { ["InvoiceNumber"] = 0.90 },
                RawText = "Amount test"
            });

        var result = await sut.ProcessAsync(document, invoice);

        invoice.TotalAmount.Should().Be(expected);
    }

    // ─── Generic Exception Handling ───────────────────────────────────

    [Fact]
    public async Task ProcessAsync_UnhandledException_ReturnsFailure()
    {
        var (document, invoice) = CreateTestEntities();
        var sut = CreateSut();

        _mockOcrService.ExtractFromImageAsync(
                Arg.Any<string>(),
                Arg.Any<OcrExtractionOptions?>(),
                Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("Something unexpected happened"));

        var result = await sut.ProcessAsync(document, invoice);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Something unexpected happened");
    }

    // ─── Processing Time ──────────────────────────────────────────────

    [Fact]
    public async Task ProcessAsync_SetsProcessingTimeInResult()
    {
        var (document, invoice) = CreateTestEntities();
        var sut = CreateSut();

        _mockOcrService.ExtractFromImageAsync(
                Arg.Any<string>(),
                Arg.Any<OcrExtractionOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(new OcrExtractionResult
            {
                Fields = new Dictionary<string, string> { ["InvoiceNumber"] = "INV-001" },
                Confidence = 0.90,
                FieldConfidences = new Dictionary<string, double> { ["InvoiceNumber"] = 0.90 },
                RawText = "Invoice"
            });

        var result = await sut.ProcessAsync(document, invoice);

        result.ProcessingTimeMs.Should().BeGreaterThanOrEqualTo(0);
    }
}
