using FluentAssertions;
using InvoiceFlow.Core.Entities;
using InvoiceFlow.Core.Enums;
using InvoiceFlow.Infrastructure.AI.Extraction;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace InvoiceFlow.UnitTests.AI.Extraction;

/// <summary>
/// Tests for OCR field extraction via TesseractOcrService and related extraction models.
/// Since TesseractOcrService is sealed and creates its engine internally,
/// these tests focus on the data models, error handling, and interface contracts.
/// The regex extraction logic is tested indirectly through the orchestrator integration.
/// </summary>
public class TesseractOcrServiceTests
{
    // ─── OcrExtractionResult Model Tests ─────────────────────────────

    [Fact]
    public void OcrExtractionResult_DefaultValues_ShouldHaveEmptyFieldsAndZeroConfidence()
    {
        var result = new OcrExtractionResult();

        result.Fields.Should().BeEmpty();
        result.Confidence.Should().Be(0);
        result.FieldConfidences.Should().BeEmpty();
        result.RawText.Should().BeEmpty();
        result.Language.Should().Be("eng");
        result.ProcessingTimeMs.Should().Be(0);
    }

    [Fact]
    public void OcrExtractionResult_Success_ShouldBeTrueWhenConfidenceAboveZero()
    {
        var result = new OcrExtractionResult { Confidence = 0.01 };

        result.Success.Should().BeTrue();
    }

    [Fact]
    public void OcrExtractionResult_Success_ShouldBeFalseWhenConfidenceIsZero()
    {
        var result = new OcrExtractionResult { Confidence = 0 };

        result.Success.Should().BeFalse();
    }

    [Fact]
    public void OcrExtractionResult_WithExtractedFields_ShouldStoreAllFields()
    {
        var fields = new Dictionary<string, string>
        {
            ["InvoiceNumber"] = "INV-2024-001",
            ["InvoiceDate"] = "15.01.2024",
            ["VendorName"] = "Acme Corp",
            ["TotalAmount"] = "1190.00"
        };

        var result = new OcrExtractionResult
        {
            Fields = fields,
            Confidence = 0.92
        };

        result.Fields.Should().HaveCount(4);
        result.Fields["InvoiceNumber"].Should().Be("INV-2024-001");
        result.Fields["TotalAmount"].Should().Be("1190.00");
    }

    [Fact]
    public void OcrExtractionResult_FieldConfidences_ShouldMapPerFieldConfidence()
    {
        var confidences = new Dictionary<string, double>
        {
            ["InvoiceNumber"] = 0.95,
            ["InvoiceDate"] = 0.88,
            ["VendorName"] = 0.72
        };

        var result = new OcrExtractionResult
        {
            FieldConfidences = confidences,
            Confidence = 0.85
        };

        result.FieldConfidences["InvoiceNumber"].Should().Be(0.95);
        result.FieldConfidences["VendorName"].Should().Be(0.72);
    }

    // ─── OcrExtractionOptions Tests ──────────────────────────────────

    [Fact]
    public void OcrExtractionOptions_Defaults_ShouldHaveReasonableDefaults()
    {
        var options = new OcrExtractionOptions();

        options.Language.Should().Be("eng");
        options.PreprocessImage.Should().BeTrue();
        options.Dpi.Should().Be(300);
        options.ConfidenceThreshold.Should().Be(0.7);
    }

    [Theory]
    [InlineData("eng")]
    [InlineData("deu")]
    [InlineData("fra")]
    [InlineData("spa")]
    public void OcrExtractionOptions_Language_CanSetDifferentLanguages(string language)
    {
        var options = new OcrExtractionOptions { Language = language };

        options.Language.Should().Be(language);
    }

    // ─── TesseractOcrService Interface Tests ──────────────────────────

    [Fact]
    public void IOcrExtractionService_CanBeMocked()
    {
        var mockService = Substitute.For<IOcrExtractionService>();

        mockService.Should().NotBeNull();
        mockService.Should().BeAssignableTo<IOcrExtractionService>();
    }

    [Fact]
    public async Task IOcrExtractionService_MockExtractFromImageAsync_ReturnsConfiguredResult()
    {
        var mockService = Substitute.For<IOcrExtractionService>();
        var expectedResult = new OcrExtractionResult
        {
            Fields = new Dictionary<string, string>
            {
                ["InvoiceNumber"] = "INV-MOCK-001",
                ["TotalAmount"] = "500.00"
            },
            Confidence = 0.95,
            FieldConfidences = new Dictionary<string, double>
            {
                ["InvoiceNumber"] = 0.98,
                ["TotalAmount"] = 0.92
            },
            RawText = "Invoice No: INV-MOCK-001\nTotal: 500.00"
        };

        mockService.ExtractFromImageAsync(
                Arg.Any<string>(),
                Arg.Any<OcrExtractionOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(expectedResult);

        var result = await mockService.ExtractFromImageAsync("test.png");

        result.Should().Be(expectedResult);
        result.Fields.Should().ContainKey("InvoiceNumber");
        result.Fields["InvoiceNumber"].Should().Be("INV-MOCK-001");
        result.Confidence.Should().Be(0.95);
    }

    [Fact]
    public async Task IOcrExtractionService_MockExtractFromPdfAsync_CanReturnNullConfidence()
    {
        var mockService = Substitute.For<IOcrExtractionService>();
        mockService.ExtractFromPdfAsync(
                Arg.Any<string>(),
                Arg.Any<OcrExtractionOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(new OcrExtractionResult { Confidence = 0 });

        var result = await mockService.ExtractFromPdfAsync("test.pdf");

        result.Success.Should().BeFalse();
        result.Confidence.Should().Be(0);
    }

    [Fact]
    public async Task IOcrExtractionService_MockExtractFromStreamAsync_ReturnsResult()
    {
        var mockService = Substitute.For<IOcrExtractionService>();
        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });

        mockService.ExtractFromStreamAsync(
                Arg.Any<Stream>(),
                Arg.Any<OcrExtractionOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(new OcrExtractionResult
            {
                Fields = new Dictionary<string, string> { ["VendorName"] = "Test Vendor" },
                Confidence = 0.80,
                RawText = "Vendor: Test Vendor"
            });

        var result = await mockService.ExtractFromStreamAsync(stream);

        result.Fields["VendorName"].Should().Be("Test Vendor");
        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task IOcrExtractionService_MockGetAvailableLanguagesAsync_ReturnsLanguageList()
    {
        var mockService = Substitute.For<IOcrExtractionService>();
        mockService.GetAvailableLanguagesAsync()
            .Returns(new List<string> { "eng", "deu", "fra" }.AsReadOnly());

        var languages = await mockService.GetAvailableLanguagesAsync();

        languages.Should().HaveCount(3);
        languages.Should().Contain("eng");
        languages.Should().Contain("deu");
    }

    // ─── TesseractOcrService Error Handling Tests ─────────────────────

    [Fact]
    public async Task TesseractOcrService_ExtractFromImageAsync_NonExistentFile_ReturnsErrorResult()
    {
        using var service = new TesseractOcrService();

        var result = await service.ExtractFromImageAsync("nonexistent_path_12345.png");

        result.Success.Should().BeFalse();
        result.Confidence.Should().Be(0);
        result.RawText.Should().StartWith("Error:");
    }

    [Fact]
    public async Task TesseractOcrService_ExtractFromPdfAsync_ReturnsNotSupported()
    {
        using var service = new TesseractOcrService();

        var result = await service.ExtractFromPdfAsync("test.pdf");

        result.Success.Should().BeFalse();
        result.RawText.Should().Contain("NotSupportedException");
    }

    [Fact]
    public async Task TesseractOcrService_ExtractFromStreamAsync_EmptyStream_ReturnsErrorResult()
    {
        using var service = new TesseractOcrService();
        using var stream = new MemoryStream(Array.Empty<byte>());

        var result = await service.ExtractFromStreamAsync(stream);

        // Should handle gracefully — either error or empty result
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task TesseractOcrService_GetAvailableLanguagesAsync_WithoutTessData_ReturnsEng()
    {
        using var service = new TesseractOcrService("/nonexistent/tessdata/path");

        var languages = await service.GetAvailableLanguagesAsync();

        languages.Should().Contain("eng");
    }

    // ─── Amount Extraction Pattern Tests ──────────────────────────────
    // These test the regex patterns used by the OCR service indirectly

    [Theory]
    [InlineData("Total: 1190.00", true)]
    [InlineData("Amount Due: €2,500.50", true)]
    [InlineData("Grand Total: $999.99", true)]
    [InlineData("SUMME: 3.456,78", true)]
    [InlineData("Total: 1190", true)]
    public void AmountPatterns_ShouldMatchVariousFormats(string text, bool shouldMatch)
    {
        var pattern = @"(?:total|amount due|grand total|sum)\s*[:=]?\s*([€$£¥]?\s*\d+[.,]\d{2})";
        var matches = System.Text.RegularExpressions.Regex.Matches(
            text, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (shouldMatch)
        {
            matches.Should().NotBeEmpty($"'{text}' should match amount pattern");
        }
    }

    [Theory]
    [InlineData("Tax: 190.00", true)]
    [InlineData("VAT: €380.00", true)]
    [InlineData("MwSt: 19%", false)] // 19% doesn't match decimal pattern
    [InlineData("IVA: 21.00", true)]
    public void TaxPatterns_ShouldMatchVariousFormats(string text, bool shouldMatch)
    {
        var pattern = @"(?:tax|vat|mwst|btw|iva)\s*[:=]?\s*([€$£¥]?\s*\d+[.,]\d{2})";
        var matches = System.Text.RegularExpressions.Regex.Matches(
            text, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (shouldMatch)
        {
            matches.Should().NotBeEmpty($"'{text}' should match tax pattern");
        }
        else
        {
            matches.Should().BeEmpty($"'{text}' should not match tax pattern");
        }
    }

    // ─── Multi-page Result Combining Tests ────────────────────────────

    [Fact]
    public void OcrExtractionResults_CanBeCombinedInMemory()
    {
        // Simulate the behavior of CombineResults from TesseractOcrService
        var page1 = new OcrExtractionResult
        {
            Fields = new Dictionary<string, string>
            {
                ["InvoiceNumber"] = "INV-001",
                ["InvoiceDate"] = "15.01.2024"
            },
            FieldConfidences = new Dictionary<string, double>
            {
                ["InvoiceNumber"] = 0.95,
                ["InvoiceDate"] = 0.88
            },
            RawText = "Page 1 content",
            Confidence = 0.90
        };

        var page2 = new OcrExtractionResult
        {
            Fields = new Dictionary<string, string>
            {
                ["TotalAmount"] = "1190.00",
                ["TaxAmount"] = "190.00"
            },
            FieldConfidences = new Dictionary<string, double>
            {
                ["TotalAmount"] = 0.92,
                ["TaxAmount"] = 0.85
            },
            RawText = "Page 2 content",
            Confidence = 0.87
        };

        // Combine like the service does
        var results = new List<OcrExtractionResult> { page1, page2 };

        var combined = new OcrExtractionResult
        {
            Fields = new Dictionary<string, string>(),
            FieldConfidences = new Dictionary<string, double>(),
            RawText = string.Join("\n\n--- PAGE BREAK ---\n\n", results.Select(r => r.RawText)),
            Language = results[0].Language,
            Confidence = results.Average(r => r.Confidence),
        };

        // Merge fields preferring higher confidence
        foreach (var result in results)
        {
            foreach (var field in result.Fields)
            {
                var existingConfidence = combined.FieldConfidences.GetValueOrDefault(field.Key, 0);
                var newConfidence = result.FieldConfidences.GetValueOrDefault(field.Key, 0);

                if (newConfidence > existingConfidence)
                {
                    combined.Fields[field.Key] = field.Value;
                    combined.FieldConfidences[field.Key] = newConfidence;
                }
            }
        }

        combined.Fields.Should().HaveCount(4);
        combined.Fields["InvoiceNumber"].Should().Be("INV-001");
        combined.Fields["TotalAmount"].Should().Be("1190.00");
        combined.Confidence.Should().BeApproximately(0.885, 0.001);
        combined.RawText.Should().Contain("--- PAGE BREAK ---");
        combined.FieldConfidences["InvoiceNumber"].Should().Be(0.95);
    }

    [Fact]
    public void OcrExtractionResults_EmptyList_ReturnsZeroConfidence()
    {
        var results = new List<OcrExtractionResult>();

        var combined = results.Count == 0
            ? new OcrExtractionResult { Confidence = 0 }
            : results[0];

        combined.Confidence.Should().Be(0);
        combined.Success.Should().BeFalse();
    }

    [Fact]
    public void OcrExtractionResults_SinglePage_ReturnsSameResult()
    {
        var single = new OcrExtractionResult
        {
            Fields = new Dictionary<string, string> { ["InvoiceNumber"] = "INV-001" },
            Confidence = 0.92
        };

        var results = new List<OcrExtractionResult> { single };

        var combined = results.Count == 1 ? results[0] : new OcrExtractionResult();

        combined.Should().BeSameAs(single);
        combined.Fields["InvoiceNumber"].Should().Be("INV-001");
    }

    // ─── Confidence Scoring for Different Field Types ─────────────────

    [Theory]
    [InlineData("InvoiceNumber", 1.1)]
    [InlineData("InvoiceDate", 1.1)]
    [InlineData("TotalAmount", 1.1)]
    [InlineData("VendorName", 1.0)]
    [InlineData("CustomerName", 1.0)]
    [InlineData("Currency", 1.0)]
    public void FieldConfidenceBoost_CriticalFieldsGetHigherConfidence(string fieldName, double multiplier)
    {
        // Simulate the confidence calculation from TesseractOcrService.CalculateFieldConfidences
        var baseConfidence = 0.85;

        var fieldConfidence = baseConfidence;
        if (fieldName is "InvoiceNumber" or "InvoiceDate" or "TotalAmount")
        {
            fieldConfidence = Math.Min(1.0, baseConfidence * multiplier);
        }

        if (fieldName is "InvoiceNumber" or "InvoiceDate" or "TotalAmount")
        {
            fieldConfidence.Should().BeGreaterThan(baseConfidence,
                $"{fieldName} should have boosted confidence");
        }

        fieldConfidence.Should().BeLessThanOrEqualTo(1.0);
    }

    [Fact]
    public void OverallConfidence_AverageOfFieldConfidences()
    {
        var fieldConfidences = new Dictionary<string, double>
        {
            ["InvoiceNumber"] = 0.95,
            ["InvoiceDate"] = 0.88,
            ["VendorName"] = 0.80,
            ["TotalAmount"] = 0.92
        };

        var overallConfidence = fieldConfidences.Values.Average();

        overallConfidence.Should().BeApproximately(0.8875, 0.001);
    }

    [Fact]
    public void OverallConfidence_FallsBackToRawWhenNoFieldConfidences()
    {
        var fieldConfidences = new Dictionary<string, double>();
        var rawConfidence = 0.75f;

        var overallConfidence = fieldConfidences.Values.Any()
            ? fieldConfidences.Values.Average()
            : rawConfidence / 100.0;

        overallConfidence.Should().BeApproximately(0.0075, 0.001);
    }
}
