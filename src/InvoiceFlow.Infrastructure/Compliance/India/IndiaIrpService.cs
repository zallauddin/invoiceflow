using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using InvoiceFlow.Core.Entities;
using InvoiceFlow.Core.Interfaces;
using InvoiceFlow.Core.Models.Compliance;
using InvoiceFlow.Infrastructure.Compliance.India.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InvoiceFlow.Infrastructure.Compliance.India;

/// <summary>
/// Indian IRP e-Invoice compliance service — generates GSTN JSON, and submits to the IRP.
/// </summary>
public sealed class IndiaIrpService : IIndiaIrpService
{
    private readonly HttpClient _httpClient;
    private readonly IndiaIrpConfig _config;
    private readonly ILogger<IndiaIrpService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    /// <summary>Initializes a new instance of the <see cref="IndiaIrpService"/> class.</summary>
    public IndiaIrpService(
        HttpClient httpClient,
        IOptions<IndiaIrpConfig> config,
        ILogger<IndiaIrpService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _config = config?.Value ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public string GenerateEinvoiceJson(Invoice invoice)
    {
        var request = BuildEinvoiceRequest(invoice);
        return JsonSerializer.Serialize(request, JsonOptions);
    }

    /// <inheritdoc/>
    public async Task<ClearanceResult> SubmitEinvoiceAsync(Invoice invoice, CancellationToken ct = default)
    {
        _logger.LogInformation("Submitting e-Invoice for invoice {InvoiceNumber}", invoice.InvoiceNumber);

        if (_config.SandboxMode)
        {
            _logger.LogInformation("Sandbox mode — simulating e-Invoice registration for invoice {InvoiceNumber}", invoice.InvoiceNumber);
            var irn = $"IRN-SANDBOX-{Guid.NewGuid():N}".ToUpperInvariant();
            return new ClearanceResult
            {
                Cleared = true,
                ClearanceId = irn,
                Timestamp = DateTime.UtcNow,
                ProviderResponse = JsonSerializer.Serialize(new
                {
                    status = "active",
                    AckNo = Random.Shared.NextInt64(10000000000, 99999999999),
                    AckDt = DateTime.UtcNow.ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture),
                    Irn = irn
                }, JsonOptions)
            };
        }

        var json = GenerateEinvoiceJson(invoice);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var endpoint = $"{_config.ApiBaseUrl.TrimEnd('/')}/eapi/v1/invoice";

        try
        {
            // Authenticate with client credentials
            var authHeader = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_config.ClientId}:{_config.ClientSecret}"));
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authHeader);

            var response = await _httpClient.PostAsync(endpoint, content, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            if (response.IsSuccessStatusCode)
            {
                var irpResponse = JsonSerializer.Deserialize<IndiaEinvoiceResponse>(responseBody, JsonOptions);
                return new ClearanceResult
                {
                    Cleared = true,
                    ClearanceId = irpResponse?.Irn,
                    Timestamp = DateTime.UtcNow,
                    ProviderResponse = responseBody
                };
            }

            _logger.LogWarning("IRP returned status {StatusCode} for invoice {InvoiceNumber}", response.StatusCode, invoice.InvoiceNumber);
            return new ClearanceResult
            {
                Cleared = false,
                ErrorMessage = $"IRP returned status {response.StatusCode}",
                Timestamp = DateTime.UtcNow,
                ProviderResponse = responseBody
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error submitting e-Invoice for invoice {InvoiceNumber}", invoice.InvoiceNumber);
            return new ClearanceResult
            {
                Cleared = false,
                ErrorMessage = $"HTTP error communicating with IRP: {ex.Message}",
                Timestamp = DateTime.UtcNow
            };
        }
    }

    /// <summary>
    /// Builds an <see cref="IndiaEinvoiceRequest"/> from the core <see cref="Invoice"/> entity.
    /// Uses CGST 9% + SGST 9% = 18% GST (simplified; real rates vary by HSN code).
    /// </summary>
    private IndiaEinvoiceRequest BuildEinvoiceRequest(Invoice invoice)
    {
        const decimal cgstRate = 9.0m;
        const decimal sgstRate = 9.0m;

        var items = invoice.Lines.Select((line, index) =>
        {
            var cgstAmt = Math.Round(line.LineTotal * cgstRate / 100m, 2);
            var sgstAmt = Math.Round(line.LineTotal * sgstRate / 100m, 2);

            return new IndiaItemDtls
            {
                SlNo = (index + 1).ToString(CultureInfo.InvariantCulture),
                PrdDesc = line.Description,
                HsnCd = line.HsnCode ?? "998314",
                Qty = line.Quantity,
                QtyUqc = "NOS",
                UnitPrice = line.UnitPrice,
                TotAmt = line.LineTotal,
                AssAmt = line.LineTotal,
                GstRt = cgstRate + sgstRate,
                CgstAmt = cgstAmt,
                SgstAmt = sgstAmt,
                TotItemVal = line.LineTotal + cgstAmt + sgstAmt
            };
        }).ToList();

        var totalCgst = items.Sum(i => i.CgstAmt);
        var totalSgst = items.Sum(i => i.SgstAmt);

        return new IndiaEinvoiceRequest
        {
            TransactionDtls = new IndiaTransactionDtls
            {
                Typ = "1",
                Irflgstyp = "R",
                Suptyp = "B2B"
            },
            DocDtls = new IndiaDocDtls
            {
                DocNo = invoice.InvoiceNumber,
                DocDt = invoice.InvoiceDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
                Typ = "INV"
            },
            SellerDtls = new IndiaPartyDtls
            {
                Gstin = _config?.GstIn ?? string.Empty,
                LglNm = invoice.VendorName,
                Addr1 = string.Empty,
                Addr2 = string.Empty,
                Place = string.Empty,
                Pin = 0,
                Stcd = string.Empty
            },
            BuyerDtls = new IndiaPartyDtls
            {
                Gstin = invoice.BuyerTaxId ?? string.Empty,
                LglNm = invoice.BuyerName,
                Addr1 = string.Empty,
                Addr2 = string.Empty,
                Place = string.Empty,
                Pin = 0,
                Stcd = string.Empty
            },
            ItemList = items,
            ValDtls = new IndiaValDtls
            {
                AssVal = invoice.Subtotal,
                CgstVal = totalCgst,
                SgstVal = totalSgst,
                TotInvVal = invoice.TotalAmount + totalCgst + totalSgst
            }
        };
    }
}
