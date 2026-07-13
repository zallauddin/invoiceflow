using System.Net;
using System.Text;
using FluentAssertions;
using InvoiceFlow.Infrastructure.AI.Extraction;
using NSubstitute;

namespace InvoiceFlow.UnitTests.AI.Extraction;

/// <summary>
/// Tests for LLM response parsing in LlmExtractionService.
/// Uses a mock HttpMessageHandler to intercept HTTP calls and return predefined LLM responses.
/// </summary>
public class LlmExtractionServiceTests : IDisposable
{
    private readonly MockHttpMessageHandler _mockHandler;

    public LlmExtractionServiceTests()
    {
        _mockHandler = new MockHttpMessageHandler("{}");
    }

    public void Dispose()
    {
        _mockHandler.Dispose();
    }

    // ─── JSON Response Parsing with _confidence Suffix Fields ─────────

    [Fact]
    public async Task ExtractFromTextAsync_WithConfidenceSuffixFields_ParsesFieldConfidences()
    {
        var llmResponse = """
        {
            "InvoiceNumber": "INV-2024-001",
            "InvoiceNumber_confidence": "0.95",
            "InvoiceDate": "2024-01-15",
            "InvoiceDate_confidence": "0.88",
            "VendorName": "Acme Corp",
            "VendorName_confidence": "0.72",
            "TotalAmount": "1190.00",
            "TotalAmount_confidence": "0.91"
        }
        """;

        _mockHandler.ResponseJson = llmResponse;
        var service = new LlmExtractionService(_mockHandler.CreateHttpClient());

        var result = await service.ExtractFromTextAsync("Sample invoice text");

        result.Fields.Should().ContainKey("InvoiceNumber");
        result.Fields["InvoiceNumber"].Should().Be("INV-2024-001");
        result.Fields.Should().ContainKey("VendorName");
        result.Fields["VendorName"].Should().Be("Acme Corp");

        // Confidence fields should be parsed separately
        result.FieldConfidences.Should().ContainKey("InvoiceNumber");
        result.FieldConfidences["InvoiceNumber"].Should().BeApproximately(0.95, 0.001);
        result.FieldConfidences.Should().ContainKey("InvoiceDate");
        result.FieldConfidences["InvoiceDate"].Should().BeApproximately(0.88, 0.001);
        result.FieldConfidences.Should().ContainKey("VendorName");
        result.FieldConfidences["VendorName"].Should().BeApproximately(0.72, 0.001);

        // Overall confidence should be the average
        result.Confidence.Should().BeApproximately(0.8625, 0.001);
    }

    [Fact]
    public async Task ExtractFromTextAsync_ConfidenceValuesClamped_BetweenZeroAndOne()
    {
        var llmResponse = """
        {
            "InvoiceNumber": "INV-001",
            "InvoiceNumber_confidence": "1.5",
            "VendorName": "Test",
            "VendorName_confidence": "-0.3"
        }
        """;

        _mockHandler.ResponseJson = llmResponse;
        var service = new LlmExtractionService(_mockHandler.CreateHttpClient());

        var result = await service.ExtractFromTextAsync("Invoice text");

        result.FieldConfidences["InvoiceNumber"].Should().Be(1.0); // clamped from 1.5
        result.FieldConfidences["VendorName"].Should().Be(0.0); // clamped from -0.3
    }

    [Fact]
    public async Task ExtractFromTextAsync_NoConfidenceFields_ConfidenceIsZero()
    {
        var llmResponse = """
        {
            "InvoiceNumber": "INV-001",
            "TotalAmount": "500.00"
        }
        """;

        _mockHandler.ResponseJson = llmResponse;
        var service = new LlmExtractionService(_mockHandler.CreateHttpClient());

        var result = await service.ExtractFromTextAsync("Invoice text");

        result.Fields.Should().HaveCount(2);
        result.FieldConfidences.Should().BeEmpty();
        result.Confidence.Should().Be(0);
    }

    // ─── Markdown Code Block Extraction ───────────────────────────────

    [Fact]
    public async Task ExtractFromTextAsync_JsonWrappedInMarkdownCodeBlock_ExtractsJson()
    {
        var llmResponse = """
        Here is the extracted invoice data:

        ```json
        {
            "InvoiceNumber": "INV-2024-042",
            "VendorName": "Widget Ltd",
            "TotalAmount": "2500.00",
            "TotalAmount_confidence": "0.93"
        }
        ```

        I hope this helps!
        """;

        _mockHandler.ResponseJson = llmResponse;
        var service = new LlmExtractionService(_mockHandler.CreateHttpClient());

        var result = await service.ExtractFromTextAsync("Invoice text");

        result.Fields.Should().ContainKey("InvoiceNumber");
        result.Fields["InvoiceNumber"].Should().Be("INV-2024-042");
        result.Fields["VendorName"].Should().Be("Widget Ltd");
        result.Fields["TotalAmount"].Should().Be("2500.00");
        result.FieldConfidences["TotalAmount"].Should().BeApproximately(0.93, 0.001);
    }

    [Fact]
    public async Task ExtractFromTextAsync_JsonWrappedInMarkdownWithoutLanguageTag_ExtractsJson()
    {
        var llmResponse = """
        ```
        {
            "InvoiceNumber": "INV-999",
            "TotalAmount": "100.00"
        }
        ```
        """;

        _mockHandler.ResponseJson = llmResponse;
        var service = new LlmExtractionService(_mockHandler.CreateHttpClient());

        var result = await service.ExtractFromTextAsync("Invoice text");

        result.Fields.Should().ContainKey("InvoiceNumber");
        result.Fields["InvoiceNumber"].Should().Be("INV-999");
    }

    [Fact]
    public async Task ExtractFromTextAsync_BareJsonWithoutCodeBlock_ParsesDirectly()
    {
        var llmResponse = """
        {
            "InvoiceNumber": "INV-BARE-001",
            "VendorName": "Direct Corp",
            "TotalAmount": "750.50"
        }
        """;

        _mockHandler.ResponseJson = llmResponse;
        var service = new LlmExtractionService(_mockHandler.CreateHttpClient());

        var result = await service.ExtractFromTextAsync("Invoice text");

        result.Fields["InvoiceNumber"].Should().Be("INV-BARE-001");
        result.Fields["VendorName"].Should().Be("Direct Corp");
    }

    // ─── Malformed JSON Handling ──────────────────────────────────────

    [Fact]
    public async Task ExtractFromTextAsync_MalformedJson_ReturnsEmptyResult()
    {
        _mockHandler.ResponseJson = "This is not JSON at all, just random text from the LLM.";
        var service = new LlmExtractionService(_mockHandler.CreateHttpClient());

        var result = await service.ExtractFromTextAsync("Invoice text");

        result.Success.Should().BeFalse();
        result.Confidence.Should().Be(0);
        result.Fields.Should().BeEmpty();
    }

    [Fact]
    public async Task ExtractFromTextAsync_EmptyResponse_ReturnsEmptyResult()
    {
        _mockHandler.ResponseJson = "";
        var service = new LlmExtractionService(_mockHandler.CreateHttpClient());

        var result = await service.ExtractFromTextAsync("Invoice text");

        result.Success.Should().BeFalse();
        result.Fields.Should().BeEmpty();
    }

    [Fact]
    public async Task ExtractFromTextAsync_PartialJson_ReturnsEmptyResult()
    {
        _mockHandler.ResponseJson = """{"InvoiceNumber": "INV-001","""; // incomplete JSON
        var service = new LlmExtractionService(_mockHandler.CreateHttpClient());

        var result = await service.ExtractFromTextAsync("Invoice text");

        result.Success.Should().BeFalse();
        result.Confidence.Should().Be(0);
    }

    // ─── Confidence Calculation ───────────────────────────────────────

    [Fact]
    public async Task ExtractFromTextAsync_MultipleConfidenceScores_AveragedCorrectly()
    {
        var llmResponse = """
        {
            "InvoiceNumber": "INV-001",
            "InvoiceNumber_confidence": "0.90",
            "InvoiceDate": "2024-03-20",
            "InvoiceDate_confidence": "0.80",
            "VendorName": "Test Co",
            "VendorName_confidence": "0.70",
            "TotalAmount": "500.00",
            "TotalAmount_confidence": "1.00"
        }
        """;

        _mockHandler.ResponseJson = llmResponse;
        var service = new LlmExtractionService(_mockHandler.CreateHttpClient());

        var result = await service.ExtractFromTextAsync("Invoice text");

        var expectedAverage = (0.90 + 0.80 + 0.70 + 1.00) / 4.0;
        result.Confidence.Should().BeApproximately(expectedAverage, 0.001);
    }

    [Fact]
    public async Task ExtractFromTextAsync_SingleConfidenceScore_UsesThatValue()
    {
        var llmResponse = """
        {
            "InvoiceNumber": "INV-001",
            "InvoiceNumber_confidence": "0.85"
        }
        """;

        _mockHandler.ResponseJson = llmResponse;
        var service = new LlmExtractionService(_mockHandler.CreateHttpClient());

        var result = await service.ExtractFromTextAsync("Invoice text");

        result.Confidence.Should().BeApproximately(0.85, 0.001);
    }

    // ─── Provider-Specific Response Format Tests ──────────────────────

    [Theory]
    [InlineData(LlmProvider.Anthropic)]
    [InlineData(LlmProvider.OpenAI)]
    [InlineData(LlmProvider.Google)]
    public async Task ExtractFromTextAsync_AllProviders_UseSameParsingLogic(LlmProvider provider)
    {
        var llmResponse = """
        {
            "InvoiceNumber": "INV-PROV-001",
            "TotalAmount": "999.99",
            "TotalAmount_confidence": "0.94"
        }
        """;

        _mockHandler.ResponseJson = llmResponse;
        var service = new LlmExtractionService(_mockHandler.CreateHttpClient());

        var result = await service.ExtractFromTextAsync(
            "Invoice text",
            new LlmExtractionOptions
            {
                Provider = provider,
                ApiKey = "test-api-key"
            });

        result.Fields.Should().ContainKey("InvoiceNumber");
        result.Fields["InvoiceNumber"].Should().Be("INV-PROV-001");
        result.Provider.Should().Be(provider);
    }

    // ─── Request Body Validation ──────────────────────────────────────

    [Fact]
    public async Task ExtractFromTextAsync_SendsCorrectContentType()
    {
        _mockHandler.ResponseJson = """{"InvoiceNumber": "INV-001"}""";
        var service = new LlmExtractionService(_mockHandler.CreateHttpClient());

        await service.ExtractFromTextAsync("Test text");

        _mockHandler.LastRequest.Should().NotBeNull();
        _mockHandler.LastRequest!.Content.Should().NotBeNull();
        var contentType = _mockHandler.LastRequest.Content!.Headers.ContentType;
        contentType!.MediaType.Should().Be("application/json");
    }

    [Fact]
    public async Task ExtractFromTextAsync_CancellationToken_Respected()
    {
        _mockHandler.ResponseJson = """{"InvoiceNumber": "INV-001"}""";
        _mockHandler.Delay = TimeSpan.FromSeconds(10);
        var service = new LlmExtractionService(_mockHandler.CreateHttpClient());

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(1));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.ExtractFromTextAsync("Test", cancellationToken: cts.Token));
    }

    // ─── Default Model Selection ──────────────────────────────────────

    [Fact]
    public async Task ExtractFromTextAsync_Anthropic_DefaultModel()
    {
        _mockHandler.ResponseJson = """{"InvoiceNumber": "INV-001"}""";
        var service = new LlmExtractionService(_mockHandler.CreateHttpClient());

        var result = await service.ExtractFromTextAsync(
            "text",
            new LlmExtractionOptions { Provider = LlmProvider.Anthropic, ApiKey = "test-key" });

        result.Provider.Should().Be(LlmProvider.Anthropic);
        // Default model is set in the service; we verify the provider metadata
        result.Model.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ExtractFromTextAsync_OpenAI_SetsModelInResult()
    {
        _mockHandler.ResponseJson = """{"InvoiceNumber": "INV-001"}""";
        var service = new LlmExtractionService(_mockHandler.CreateHttpClient());

        var result = await service.ExtractFromTextAsync(
            "text",
            new LlmExtractionOptions
            {
                Provider = LlmProvider.OpenAI,
                Model = "gpt-4o-mini",
                ApiKey = "test-key"
            });

        result.Model.Should().Be("gpt-4o-mini");
    }

    // ─── Line Items Parsing ───────────────────────────────────────────

    [Fact]
    public async Task ExtractFromTextAsync_WithLineItems_ParsesArrayFields()
    {
        var llmResponse = """
        {
            "InvoiceNumber": "INV-001",
            "LineItems": "[{\"Description\": \"Widget A\", \"Quantity\": \"10\", \"UnitPrice\": \"25.00\", \"TotalPrice\": \"250.00\"}]",
            "TotalAmount_confidence": "0.90"
        }
        """;

        _mockHandler.ResponseJson = llmResponse;
        var service = new LlmExtractionService(_mockHandler.CreateHttpClient());

        var result = await service.ExtractFromTextAsync("Invoice text");

        result.Fields.Should().ContainKey("LineItems");
        result.Fields["LineItems"].Should().Contain("Widget A");
    }

    // ─── Helper: Mock HTTP Handler ────────────────────────────────────

    /// <summary>
    /// A mock HttpMessageHandler that returns a configurable JSON response.
    /// Captures the last request for assertion purposes.
    /// </summary>
    private sealed class MockHttpMessageHandler : HttpMessageHandler
    {
        public string ResponseJson { get; set; }
        public HttpRequestMessage? LastRequest { get; private set; }
        public TimeSpan Delay { get; set; } = TimeSpan.Zero;

        public MockHttpMessageHandler(string responseJson)
        {
            ResponseJson = responseJson;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;

            if (Delay > TimeSpan.Zero)
            {
                await Task.Delay(Delay, cancellationToken);
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ResponseJson, Encoding.UTF8, "application/json")
            };
        }

        public HttpClient CreateHttpClient() => new(this);
    }
}
