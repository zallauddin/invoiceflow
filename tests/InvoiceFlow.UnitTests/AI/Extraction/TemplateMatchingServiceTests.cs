using FluentAssertions;
using InvoiceFlow.Infrastructure.AI.Extraction;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace InvoiceFlow.UnitTests.AI.Extraction;

/// <summary>
/// Tests for TemplateMatchingService — template lifecycle, feature extraction,
/// regex pattern generation, and self-learning behavior.
/// </summary>
public class TemplateMatchingServiceTests
{
    private readonly ILogger<TemplateMatchingService> _logger = Substitute.For<ILogger<TemplateMatchingService>>();
    private readonly ILlmExtractionService _mockLlmService = Substitute.For<ILlmExtractionService>();

    private TemplateMatchingService CreateService(ILlmExtractionService? llmService = null)
        => new(_logger, llmService ?? _mockLlmService);

    // ─── MatchOrCreateTemplateAsync ───────────────────────────────────

    [Fact]
    public async Task MatchOrCreateTemplateAsync_NoTemplates_ReturnsNoMatch()
    {
        var service = CreateService();

        var result = await service.MatchOrCreateTemplateAsync("Sample invoice text");

        result.HasMatch.Should().BeFalse();
        result.Confidence.Should().Be(0);
        result.Template.Should().BeNull();
    }

    [Fact]
    public async Task MatchOrCreateTemplateAsync_EmptyText_ReturnsNoMatch()
    {
        var service = CreateService();

        var result = await service.MatchOrCreateTemplateAsync(string.Empty);

        result.HasMatch.Should().BeFalse();
        result.Confidence.Should().Be(0);
    }

    [Fact]
    public async Task MatchOrCreateTemplateAsync_WhitespaceText_ReturnsNoMatch()
    {
        var service = CreateService();

        var result = await service.MatchOrCreateTemplateAsync("   \n  \t  ");

        result.HasMatch.Should().BeFalse();
    }

    [Fact]
    public async Task MatchOrCreateTemplateAsync_WithMatchingTemplate_ReturnsMatch()
    {
        var service = CreateService();

        // Create a template first
        var template = await service.CreateTemplateAsync(
            "Acme Standard Invoice",
            "Invoice No: INV-001\nVendor: Acme Corp\nTotal: 100.00",
            new Dictionary<string, string>
            {
                ["InvoiceNumber"] = "INV-001",
                ["VendorName"] = "Acme Corp",
                ["TotalAmount"] = "100.00"
            });

        // Now match with similar text
        var result = await service.MatchOrCreateTemplateAsync(
            "Invoice No: INV-002\nVendor: Acme Corp\nTotal: 200.00");

        result.HasMatch.Should().BeTrue();
        result.Template.Should().NotBeNull();
        result.Template!.Name.Should().Be("Acme Standard Invoice");
        result.Confidence.Should().BeGreaterThan(0);
    }

    // ─── CreateTemplateAsync ──────────────────────────────────────────

    [Fact]
    public async Task CreateTemplateAsync_CreatesTemplateWithName()
    {
        var service = CreateService();
        var extractedFields = new Dictionary<string, string>
        {
            ["InvoiceNumber"] = "INV-2024-001",
            ["VendorName"] = "Test Corp",
            ["TotalAmount"] = "1500.00"
        };

        var template = await service.CreateTemplateAsync(
            "Test Template",
            "Invoice No: INV-2024-001\nVendor: Test Corp\nTotal: 1500.00",
            extractedFields);

        template.Name.Should().Be("Test Template");
        template.Description.Should().Be("Auto-generated template from invoice extraction");
        template.UsageCount.Should().Be(1);
    }

    [Fact]
    public async Task CreateTemplateAsync_GeneratesFieldMappings()
    {
        var service = CreateService();
        var extractedFields = new Dictionary<string, string>
        {
            ["InvoiceNumber"] = "INV-001",
            ["TotalAmount"] = "999.99",
            ["InvoiceDate"] = "15.01.2024"
        };

        var template = await service.CreateTemplateAsync(
            "Test",
            "Invoice text here",
            extractedFields);

        template.FieldMappings.Should().ContainKey("InvoiceNumber");
        template.FieldMappings.Should().ContainKey("TotalAmount");
        template.FieldMappings.Should().ContainKey("InvoiceDate");
    }

    [Fact]
    public async Task CreateTemplateAsync_ExtractsLayoutFeatures()
    {
        var service = CreateService();
        var rawText = """
            Invoice No: INV-001
            Invoice Date: 15.01.2024
            Vendor: Acme Corp
            Bill To: Customer Inc
            Description    Qty    Unit Price    Amount
            Widget A       10     25.00         250.00
            Widget B        5     50.00         250.00
            Total: 500.00
            VAT: 100.00
            Amount Due: 600.00
            """;

        var template = await service.CreateTemplateAsync("Layout Test", rawText, new Dictionary<string, string>());

        template.VisualFeatures.Should().ContainKey("header_invoice_number");
        template.VisualFeatures["header_invoice_number"].Should().Be("present");
        template.VisualFeatures.Should().ContainKey("header_dates");
        template.VisualFeatures["header_dates"].Should().Be("present");
        template.VisualFeatures.Should().ContainKey("vendor_block");
        template.VisualFeatures.Should().ContainKey("customer_block");
        template.VisualFeatures.Should().ContainKey("totals_block");
        template.VisualFeatures.Should().ContainKey("tax_block");
    }

    [Fact]
    public async Task CreateTemplateAsync_SetsIdAndTimestamps()
    {
        var service = CreateService();
        var before = DateTime.UtcNow;

        var template = await service.CreateTemplateAsync(
            "Timestamp Test",
            "Some text",
            new Dictionary<string, string>());

        template.Id.Should().NotBe(Guid.Empty);
        template.CreatedAt.Should().BeOnOrAfter(before);
        template.UpdatedAt.Should().BeOnOrAfter(before);
    }

    [Fact]
    public async Task CreateTemplateAsync_TruncatesLongExampleText()
    {
        var service = CreateService();
        var longText = new string('A', 1000);

        var template = await service.CreateTemplateAsync(
            "Long Text",
            longText,
            new Dictionary<string, string>());

        template.ExampleText.Length.Should().BeLessThanOrEqualTo(500);
    }

    // ─── UpdateTemplateAsync ──────────────────────────────────────────

    [Fact]
    public async Task UpdateTemplateAsync_LearnsNewFieldMappings()
    {
        var service = CreateService();

        var template = await service.CreateTemplateAsync(
            "Learn Test",
            "Invoice text",
            new Dictionary<string, string> { ["InvoiceNumber"] = "INV-001" });

        var updated = await service.UpdateTemplateAsync(
            template.Id,
            new Dictionary<string, string>
            {
                ["InvoiceNumber"] = "INV-002",
                ["CustomerName"] = "New Customer"
            },
            0.90);

        updated.FieldMappings.Should().ContainKey("CustomerName");
        updated.FieldMappings["CustomerName"].FieldName.Should().Be("CustomerName");
    }

    [Fact]
    public async Task UpdateTemplateAsync_UpdatesUsageCountAndAccuracy()
    {
        var service = CreateService();

        var template = await service.CreateTemplateAsync(
            "Accuracy Test",
            "Text",
            new Dictionary<string, string>());

        template.UsageCount.Should().Be(1);
        template.AverageAccuracy.Should().Be(0.0);

        var updated = await service.UpdateTemplateAsync(
            template.Id,
            new Dictionary<string, string>(),
            0.85);

        updated.UsageCount.Should().Be(2);
        updated.AverageAccuracy.Should().BeApproximately(0.425, 0.001); // (0.0 * 1 + 0.85) / 2
    }

    [Fact]
    public async Task UpdateTemplateAsync_MultipleUpdates_ImproveAverageAccuracy()
    {
        var service = CreateService();

        var template = await service.CreateTemplateAsync(
            "Multi Update",
            "Text",
            new Dictionary<string, string>());

        var updated1 = await service.UpdateTemplateAsync(template.Id, new Dictionary<string, string>(), 0.80);
        var updated2 = await service.UpdateTemplateAsync(template.Id, new Dictionary<string, string>(), 0.90);
        var updated3 = await service.UpdateTemplateAsync(template.Id, new Dictionary<string, string>(), 0.85);

        updated3.UsageCount.Should().Be(4);
        // Average: (0.0*1 + 0.80 + 0.90 + 0.85) / 4 = 2.55/4 = 0.6375
        updated3.AverageAccuracy.Should().BeApproximately(0.6375, 0.001);
    }

    [Fact]
    public async Task UpdateTemplateAsync_LowersConfidenceThresholdOverTime()
    {
        var service = CreateService();

        var template = await service.CreateTemplateAsync(
            "Threshold Test",
            "Text",
            new Dictionary<string, string>());

        var updated = await service.UpdateTemplateAsync(
            template.Id,
            new Dictionary<string, string>(),
            0.90);

        updated.ConfidenceThreshold.Should().BeLessThan(template.ConfidenceThreshold);
        updated.ConfidenceThreshold.Should().BeGreaterThanOrEqualTo(0.5);
    }

    [Fact]
    public async Task UpdateTemplateAsync_NonExistentTemplate_ThrowsInvalidOperation()
    {
        var service = CreateService();

        var act = () => service.UpdateTemplateAsync(
            Guid.NewGuid(),
            new Dictionary<string, string>(),
            0.90);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    // ─── GetTemplatesAsync ────────────────────────────────────────────

    [Fact]
    public async Task GetTemplatesAsync_NoTemplates_ReturnsEmptyList()
    {
        var service = CreateService();

        var templates = await service.GetTemplatesAsync();

        templates.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTemplatesAsync_WithTemplates_ReturnsOrderedByUsage()
    {
        var service = CreateService();

        await service.CreateTemplateAsync("Low Use", "Text1", new Dictionary<string, string>());
        var highUse = await service.CreateTemplateAsync("High Use", "Text2", new Dictionary<string, string>());
        await service.UpdateTemplateAsync(highUse.Id, new Dictionary<string, string>(), 0.90);
        await service.UpdateTemplateAsync(highUse.Id, new Dictionary<string, string>(), 0.90);

        var templates = await service.GetTemplatesAsync();

        templates.Should().HaveCount(2);
        templates[0].Name.Should().Be("High Use"); // Higher usage count comes first
        templates[0].UsageCount.Should().Be(3);
    }

    // ─── DeleteTemplateAsync ──────────────────────────────────────────

    [Fact]
    public async Task DeleteTemplateAsync_RemovesTemplate()
    {
        var service = CreateService();

        var template = await service.CreateTemplateAsync(
            "To Delete",
            "Text",
            new Dictionary<string, string>());

        await service.DeleteTemplateAsync(template.Id);

        var templates = await service.GetTemplatesAsync();
        templates.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteTemplateAsync_NonExistent_DoesNotThrow()
    {
        var service = CreateService();

        var act = () => service.DeleteTemplateAsync(Guid.NewGuid());

        await act.Should().NotThrowAsync();
    }

    // ─── Feature Extraction (Layout Pattern Detection) ────────────────

    [Theory]
    [InlineData("Invoice No: INV-001", "header_invoice_number", "present")]
    [InlineData("Rechnung Nr: R-001", "header_invoice_number", "present")]
    [InlineData("Some random text without invoice header", "header_invoice_number", "absent")]
    [InlineData("Invoice Date: 2024-01-15", "header_dates", "present")]
    [InlineData("Due Date: 2024-02-15", "header_dates", "present")]
    [InlineData("Vendor: Acme Corp", "vendor_block", "present")]
    [InlineData("Bill To: Customer Inc", "customer_block", "present")]
    [InlineData("Total: 1000.00", "totals_block", "present")]
    [InlineData("Amount Due: 500.00", "totals_block", "present")]
    [InlineData("VAT: 19%", "tax_block", "present")]
    [InlineData("MwSt: 19%", "tax_block", "present")]
    [InlineData("Payment Terms: Net 30", "payment_info", "present")]
    [InlineData("IBAN: DE89370400440532013000", "payment_info", "present")]
    public async Task MatchOrCreateTemplateAsync_DetectsLayoutFeatures(
        string text, string featureKey, string expectedValue)
    {
        var service = CreateService();

        // Create template from text with features, then check the features via GetTemplatesAsync
        await service.CreateTemplateAsync("Feature Test", text, new Dictionary<string, string>());

        var templates = await service.GetTemplatesAsync();
        templates.Should().ContainSingle();
        templates[0].VisualFeatures.Should().ContainKey(featureKey);
        templates[0].VisualFeatures[featureKey].Should().Be(expectedValue);
    }

    [Fact]
    public async Task MatchOrCreateTemplateAsync_DetectsLineItemStructure()
    {
        var service = CreateService();
        var text = """
            Item        Qty    Price
            Widget A    10     25.00
            Widget B     5     50.00
            """;

        var template = await service.CreateTemplateAsync("Line Items", text, new Dictionary<string, string>());

        template.VisualFeatures.Should().ContainKey("has_line_items");
        template.VisualFeatures["has_line_items"].Should().Be("structured");
    }

    [Fact]
    public async Task MatchOrCreateTemplateAsync_DetectsTotalAmountPosition()
    {
        var service = CreateService();
        var text = """
            Invoice No: INV-001
            Date: 15.01.2024
            Vendor: Acme
            Widget A    10     25.00
            Widget B     5     50.00
            Subtotal: 500.00
            Tax: 100.00
            Total: 600.00
            """;

        var template = await service.CreateTemplateAsync("Position Test", text, new Dictionary<string, string>());

        template.VisualFeatures.Should().ContainKey("total_amount_position");
        template.VisualFeatures["total_amount_position"].Should().Be("bottom");
    }

    // ─── Regex Pattern Generation ─────────────────────────────────────

    [Fact]
    public async Task CreateTemplateAsync_GeneratesAmountPatternForFinancialFields()
    {
        var service = CreateService();
        var fields = new Dictionary<string, string>
        {
            ["TotalAmount"] = "1190.00",
            ["TaxAmount"] = "190.00",
            ["Subtotal"] = "1000.00"
        };

        var template = await service.CreateTemplateAsync(
            "Amount Patterns",
            "Total: 1190.00",
            fields);

        template.FieldMappings["TotalAmount"].RegexPattern.Should().NotBeNullOrEmpty();
        template.FieldMappings["TaxAmount"].RegexPattern.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task CreateTemplateAsync_GeneratesDatePatternForDateFields()
    {
        var service = CreateService();
        var fields = new Dictionary<string, string>
        {
            ["InvoiceDate"] = "15.01.2024"
        };

        var template = await service.CreateTemplateAsync(
            "Date Patterns",
            "Date: 15.01.2024",
            fields);

        template.FieldMappings["InvoiceDate"].RegexPattern.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task CreateTemplateAsync_GeneratesTaxIdPatternForTaxIdFields()
    {
        var service = CreateService();
        var fields = new Dictionary<string, string>
        {
            ["VendorTaxId"] = "NL123456789B01"
        };

        var template = await service.CreateTemplateAsync(
            "TaxId Patterns",
            "Tax ID: NL123456789B01",
            fields);

        template.FieldMappings["VendorTaxId"].RegexPattern.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task CreateTemplateAsync_InfersDataTypeFromValues()
    {
        var service = CreateService();
        var fields = new Dictionary<string, string>
        {
            ["TotalAmount"] = "1190.00",
            ["InvoiceDate"] = "2024-01-15",
            ["InvoiceNumber"] = "INV-001",
            ["IsActive"] = "true"
        };

        var template = await service.CreateTemplateAsync(
            "Type Inference",
            "Text",
            fields);

        template.FieldMappings["TotalAmount"].DataType.Should().Be("decimal");
        template.FieldMappings["InvoiceDate"].DataType.Should().Be("date");
        template.FieldMappings["InvoiceNumber"].DataType.Should().Be("string");
        template.FieldMappings["IsActive"].DataType.Should().Be("boolean");
    }

    [Fact]
    public async Task CreateTemplateAsync_SetsRequiredFieldsCorrectly()
    {
        var service = CreateService();
        var fields = new Dictionary<string, string>
        {
            ["InvoiceNumber"] = "INV-001",
            ["InvoiceDate"] = "2024-01-15",
            ["VendorName"] = "Acme",
            ["TotalAmount"] = "1190.00",
            ["Currency"] = "EUR",
            ["Notes"] = "Some note"
        };

        var template = await service.CreateTemplateAsync(
            "Required Fields",
            "Text",
            fields);

        template.FieldMappings["InvoiceNumber"].IsRequired.Should().BeTrue();
        template.FieldMappings["InvoiceDate"].IsRequired.Should().BeTrue();
        template.FieldMappings["VendorName"].IsRequired.Should().BeTrue();
        template.FieldMappings["TotalAmount"].IsRequired.Should().BeTrue();
        template.FieldMappings["Notes"].IsRequired.Should().BeFalse();
    }

    // ─── Self-Learning: Repeated Updates Improve Template ─────────────

    [Fact]
    public async Task SelfLearning_RepeatedUpdates_IncreaseUsageCount()
    {
        var service = CreateService();
        var template = await service.CreateTemplateAsync(
            "Self-Learning",
            "Invoice text with vendor and total",
            new Dictionary<string, string>
            {
                ["InvoiceNumber"] = "INV-001",
                ["VendorName"] = "Acme Corp",
                ["TotalAmount"] = "500.00"
            });

        var initialUsage = template.UsageCount;

        for (var i = 0; i < 5; i++)
        {
            await service.UpdateTemplateAsync(
                template.Id,
                new Dictionary<string, string>
                {
                    ["InvoiceNumber"] = $"INV-{100 + i:D3}",
                    ["VendorName"] = "Acme Corp",
                    ["TotalAmount"] = $"{500 + i * 100}.00"
                },
                0.85 + i * 0.02);
        }

        var templates = await service.GetTemplatesAsync();
        var updated = templates.Single(t => t.Id == template.Id);

        updated.UsageCount.Should().Be(initialUsage + 5);
        updated.AverageAccuracy.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task SelfLearning_UpdateAddsNewFieldMappings()
    {
        var service = CreateService();
        var template = await service.CreateTemplateAsync(
            "Learning Fields",
            "Text",
            new Dictionary<string, string> { ["InvoiceNumber"] = "INV-001" });

        template.FieldMappings.Should().HaveCount(1);

        await service.UpdateTemplateAsync(
            template.Id,
            new Dictionary<string, string>
            {
                ["InvoiceNumber"] = "INV-002",
                ["VendorName"] = "New Vendor",
                ["Currency"] = "EUR"
            },
            0.90);

        var templates = await service.GetTemplatesAsync();
        var updated = templates.Single(t => t.Id == template.Id);

        updated.FieldMappings.Should().ContainKey("VendorName");
        updated.FieldMappings.Should().ContainKey("Currency");
    }

    [Fact]
    public async Task SelfLearning_UpdateDoesNotOverwriteExistingExtractionPrompt()
    {
        var service = CreateService();
        var template = await service.CreateTemplateAsync(
            "Prompt Preservation",
            "Text",
            new Dictionary<string, string> { ["InvoiceNumber"] = "INV-001" });

        var originalPrompt = template.FieldMappings["InvoiceNumber"].ExtractionPrompt;

        await service.UpdateTemplateAsync(
            template.Id,
            new Dictionary<string, string> { ["InvoiceNumber"] = "INV-002" },
            0.90);

        var templates = await service.GetTemplatesAsync();
        var updated = templates.Single(t => t.Id == template.Id);

        updated.FieldMappings["InvoiceNumber"].ExtractionPrompt.Should().Be(originalPrompt);
    }

    [Fact]
    public async Task SelfLearning_VisualFeaturesUpdatedWhenLineItemsDetected()
    {
        var service = CreateService();
        var template = await service.CreateTemplateAsync(
            "Feature Learning",
            "Text without line items",
            new Dictionary<string, string>());

        template.VisualFeatures["has_line_items"].Should().Be("unstructured");

        await service.UpdateTemplateAsync(
            template.Id,
            new Dictionary<string, string>
            {
                ["Description"] = "Widget A",
                ["LineItems"] = "[{\"Description\": \"Widget A\"}]"
            },
            0.90);

        var templates = await service.GetTemplatesAsync();
        var updated = templates.Single(t => t.Id == template.Id);

        updated.VisualFeatures["has_line_items"].Should().Be("present");
    }
}
