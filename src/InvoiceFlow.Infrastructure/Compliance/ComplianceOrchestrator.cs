using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using InvoiceFlow.Core.Entities;
using InvoiceFlow.Core.Enums;
using InvoiceFlow.Core.Interfaces;
using InvoiceFlow.Core.Models.Compliance;
using Microsoft.Extensions.Logging;

namespace InvoiceFlow.Infrastructure.Compliance;

/// <summary>
/// Routes invoices to the correct country-specific compliance handler based on
/// the configured compliance model or inferred from the invoice's country code.
/// Tracks compliance status on the invoice entity and supports post-audit archival hashing.
/// </summary>
public sealed class ComplianceOrchestrator : IComplianceOrchestrator
{
    private readonly IPeppolComplianceService _peppolService;
    private readonly IZatcaComplianceService _zatcaService;
    private readonly IBrazilNfeService _brazilNfeService;
    private readonly IIndiaIrpService _indiaIrpService;
    private readonly IMexicoCfdiService _mexicoCfdiService;
    private readonly IItalySdiService _italySdiService;
    private readonly IFrancePpfService _francePpfService;
    private readonly IPolandKsefService _polandKsefService;
    private readonly IRepository<ComplianceConfig> _configRepository;
    private readonly ILogger<ComplianceOrchestrator> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ComplianceOrchestrator"/> class.
    /// </summary>
    public ComplianceOrchestrator(
        IPeppolComplianceService peppolService,
        IZatcaComplianceService zatcaService,
        IBrazilNfeService brazilNfeService,
        IIndiaIrpService indiaIrpService,
        IMexicoCfdiService mexicoCfdiService,
        IItalySdiService italySdiService,
        IFrancePpfService francePpfService,
        IPolandKsefService polandKsefService,
        IRepository<ComplianceConfig> configRepository,
        ILogger<ComplianceOrchestrator> logger)
    {
        ArgumentNullException.ThrowIfNull(peppolService);
        ArgumentNullException.ThrowIfNull(zatcaService);
        ArgumentNullException.ThrowIfNull(brazilNfeService);
        ArgumentNullException.ThrowIfNull(indiaIrpService);
        ArgumentNullException.ThrowIfNull(mexicoCfdiService);
        ArgumentNullException.ThrowIfNull(italySdiService);
        ArgumentNullException.ThrowIfNull(francePpfService);
        ArgumentNullException.ThrowIfNull(polandKsefService);
        ArgumentNullException.ThrowIfNull(configRepository);
        ArgumentNullException.ThrowIfNull(logger);

        _peppolService = peppolService;
        _zatcaService = zatcaService;
        _brazilNfeService = brazilNfeService;
        _indiaIrpService = indiaIrpService;
        _mexicoCfdiService = mexicoCfdiService;
        _italySdiService = italySdiService;
        _francePpfService = francePpfService;
        _polandKsefService = polandKsefService;
        _configRepository = configRepository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ComplianceOrchestrationResult> ProcessAsync(Invoice invoice, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        try
        {
            var model = await ResolveComplianceModelAsync(invoice, ct);
            invoice.ComplianceModel = model;

            _logger.LogInformation(
                "Processing compliance for invoice {InvoiceId} using model {Model} (Country: {CountryCode})",
                invoice.Id, model, invoice.CountryCode);

            return model switch
            {
                ComplianceModel.Peppol => await ProcessPeppolAsync(invoice, ct),
                ComplianceModel.Zatca => await ProcessZatcaAsync(invoice, ct),
                ComplianceModel.BrazilNfe => await ProcessBrazilNfeAsync(invoice, ct),
                ComplianceModel.IndiaIrp => await ProcessIndiaIrpAsync(invoice, ct),
                ComplianceModel.MexicoCfdi => await ProcessMexicoCfdiAsync(invoice, ct),
                ComplianceModel.ItalySdi => await ProcessItalySdiAsync(invoice, ct),
                ComplianceModel.FrancePpf => await ProcessFrancePpfAsync(invoice, ct),
                ComplianceModel.PolandKsef => await ProcessPolandKsefAsync(invoice, ct),
                ComplianceModel.PostAudit => await ProcessPostAuditAsync(invoice, ct),
                _ => new ComplianceOrchestrationResult
                {
                    Success = false,
                    Model = model,
                    ErrorMessage = $"Unsupported compliance model: {model}"
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Compliance processing failed for invoice {InvoiceId}", invoice.Id);
            invoice.Status = InvoiceStatus.NonCompliant;

            return new ComplianceOrchestrationResult
            {
                Success = false,
                Model = invoice.ComplianceModel,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <inheritdoc />
    public async Task<ComplianceOrchestrationResult> CheckStatusAsync(Invoice invoice, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        var model = invoice.ComplianceModel;
        if (model is null)
        {
            return new ComplianceOrchestrationResult
            {
                Success = false,
                ErrorMessage = "Invoice has no compliance model assigned. Run ProcessAsync first."
            };
        }

        try
        {
            return model.Value switch
            {
                ComplianceModel.ItalySdi => await CheckItalySdiStatusAsync(invoice, ct),
                ComplianceModel.FrancePpf => await CheckFrancePpfStatusAsync(invoice, ct),
                ComplianceModel.PolandKsef => await CheckPolandKsefStatusAsync(invoice, ct),
                _ => GetCachedResult(invoice)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Status check failed for invoice {InvoiceId}", invoice.Id);

            return new ComplianceOrchestrationResult
            {
                Success = false,
                Model = model,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <inheritdoc />
    public Task<string> ComputeArchivalHashAsync(Invoice invoice, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        var payload = string.Join("|",
            invoice.InvoiceNumber ?? string.Empty,
            invoice.VendorName ?? string.Empty,
            invoice.BuyerName ?? string.Empty,
            invoice.TotalAmount.ToString("F2"),
            invoice.InvoiceDate.ToString("O"));

        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        var hashHex = Convert.ToHexString(hashBytes).ToLowerInvariant();

        return Task.FromResult(hashHex);
    }

    // ─── Private: Country-Code to Model Inference ──────────────────────

    private async Task<ComplianceModel> ResolveComplianceModelAsync(Invoice invoice, CancellationToken ct)
    {
        if (invoice.ComplianceModel.HasValue)
        {
            return invoice.ComplianceModel.Value;
        }

        // Try to look up explicit config for this tenant + country
        var allConfigs = await _configRepository.GetAllAsync(0, 1000, ct);
        var config = allConfigs.FirstOrDefault(c =>
            c.TenantId == invoice.TenantId &&
            c.CountryCode == invoice.CountryCode &&
            c.IsEnabled);

        if (config is not null)
        {
            return config.Model;
        }

        // Fallback: infer from country code
        return InferModelFromCountryCode(invoice.CountryCode);
    }

    private static ComplianceModel InferModelFromCountryCode(string? countryCode)
    {
        return countryCode?.ToUpperInvariant() switch
        {
            "SA" => ComplianceModel.Zatca,
            "BR" => ComplianceModel.BrazilNfe,
            "IN" => ComplianceModel.IndiaIrp,
            "MX" => ComplianceModel.MexicoCfdi,
            "IT" => ComplianceModel.ItalySdi,
            "FR" => ComplianceModel.FrancePpf,
            "PL" => ComplianceModel.PolandKsef,
            _ => ComplianceModel.Peppol
        };
    }

    // ─── Private: Peppol (2-step: validate → transmit) ─────────────────

    private async Task<ComplianceOrchestrationResult> ProcessPeppolAsync(Invoice invoice, CancellationToken ct)
    {
        var validation = await _peppolService.ValidateAsync(invoice, ct);
        if (!validation.IsValid)
        {
            invoice.Status = InvoiceStatus.NonCompliant;
            return new ComplianceOrchestrationResult
            {
                Success = false,
                Model = ComplianceModel.Peppol,
                ErrorMessage = $"PEPPOL validation failed: {string.Join("; ", validation.Errors)}",
                ProviderResponse = JsonSerializer.Serialize(validation)
            };
        }

        var ublXml = _peppolService.GenerateUblXml(invoice);
        var transmission = await _peppolService.TransmitAsync(invoice, ublXml, ct);

        if (!transmission.Success)
        {
            invoice.Status = InvoiceStatus.NonCompliant;
            return new ComplianceOrchestrationResult
            {
                Success = false,
                Model = ComplianceModel.Peppol,
                ErrorMessage = transmission.ErrorMessage,
                ProviderResponse = JsonSerializer.Serialize(transmission)
            };
        }

        invoice.ComplianceId = transmission.TransmissionId;
        invoice.ComplianceResponse = JsonSerializer.Serialize(transmission);
        invoice.TransmittedAt = transmission.Timestamp;
        invoice.Status = InvoiceStatus.Transmitted;

        return new ComplianceOrchestrationResult
        {
            Success = true,
            Model = ComplianceModel.Peppol,
            ComplianceId = transmission.TransmissionId,
            ProviderResponse = JsonSerializer.Serialize(transmission),
            Timestamp = transmission.Timestamp
        };
    }

    // ─── Private: Zatca (clearance) ────────────────────────────────────

    private async Task<ComplianceOrchestrationResult> ProcessZatcaAsync(Invoice invoice, CancellationToken ct)
    {
        var result = await _zatcaService.RequestClearanceAsync(invoice, ct);

        if (!result.Cleared)
        {
            invoice.Status = InvoiceStatus.NonCompliant;
            return new ComplianceOrchestrationResult
            {
                Success = false,
                Model = ComplianceModel.Zatca,
                ErrorMessage = result.ErrorMessage,
                ProviderResponse = JsonSerializer.Serialize(result)
            };
        }

        invoice.ComplianceId = result.ClearanceId;
        invoice.ComplianceResponse = JsonSerializer.Serialize(result);
        invoice.CompliantAt = result.Timestamp;
        invoice.Status = InvoiceStatus.Compliant;

        return new ComplianceOrchestrationResult
        {
            Success = true,
            Model = ComplianceModel.Zatca,
            ComplianceId = result.ClearanceId,
            ProviderResponse = JsonSerializer.Serialize(result),
            Timestamp = result.Timestamp
        };
    }

    // ─── Private: Brazil NF-e ──────────────────────────────────────────

    private async Task<ComplianceOrchestrationResult> ProcessBrazilNfeAsync(Invoice invoice, CancellationToken ct)
    {
        var result = await _brazilNfeService.SubmitNfeAsync(invoice, ct);
        return MapClearanceResult(invoice, ComplianceModel.BrazilNfe, result);
    }

    // ─── Private: India IRP ────────────────────────────────────────────

    private async Task<ComplianceOrchestrationResult> ProcessIndiaIrpAsync(Invoice invoice, CancellationToken ct)
    {
        var result = await _indiaIrpService.SubmitEinvoiceAsync(invoice, ct);
        return MapClearanceResult(invoice, ComplianceModel.IndiaIrp, result);
    }

    // ─── Private: Mexico CFDI ──────────────────────────────────────────

    private async Task<ComplianceOrchestrationResult> ProcessMexicoCfdiAsync(Invoice invoice, CancellationToken ct)
    {
        var result = await _mexicoCfdiService.StampCfdiAsync(invoice, ct);
        return MapClearanceResult(invoice, ComplianceModel.MexicoCfdi, result);
    }

    // ─── Private: Italy SdI (2-step: format → transmit) ────────────────

    private async Task<ComplianceOrchestrationResult> ProcessItalySdiAsync(Invoice invoice, CancellationToken ct)
    {
        var fatturaPaXml = _italySdiService.FormatFatturaPaForSdi(invoice);
        var result = await _italySdiService.TransmitAsync(invoice, fatturaPaXml, ct);

        if (!result.Accepted)
        {
            invoice.Status = InvoiceStatus.NonCompliant;
            return new ComplianceOrchestrationResult
            {
                Success = false,
                Model = ComplianceModel.ItalySdi,
                ErrorMessage = result.ErrorMessage,
                ProviderResponse = JsonSerializer.Serialize(result)
            };
        }

        invoice.ComplianceId = result.ReferenceId;
        invoice.ComplianceResponse = JsonSerializer.Serialize(result);
        invoice.TransmittedAt = result.Timestamp;
        invoice.Status = InvoiceStatus.Transmitted;

        return new ComplianceOrchestrationResult
        {
            Success = true,
            Model = ComplianceModel.ItalySdi,
            ComplianceId = result.ReferenceId,
            ProviderResponse = JsonSerializer.Serialize(result),
            Timestamp = result.Timestamp
        };
    }

    // ─── Private: France PPF ───────────────────────────────────────────

    private async Task<ComplianceOrchestrationResult> ProcessFrancePpfAsync(Invoice invoice, CancellationToken ct)
    {
        var result = await _francePpfService.ReportAsync(invoice, ct);

        if (!result.Accepted)
        {
            invoice.Status = InvoiceStatus.NonCompliant;
            return new ComplianceOrchestrationResult
            {
                Success = false,
                Model = ComplianceModel.FrancePpf,
                ErrorMessage = result.ErrorMessage,
                ProviderResponse = JsonSerializer.Serialize(result)
            };
        }

        invoice.ComplianceId = result.ReferenceId;
        invoice.ComplianceResponse = JsonSerializer.Serialize(result);
        invoice.TransmittedAt = result.Timestamp;
        invoice.Status = InvoiceStatus.Transmitted;

        return new ComplianceOrchestrationResult
        {
            Success = true,
            Model = ComplianceModel.FrancePpf,
            ComplianceId = result.ReferenceId,
            ProviderResponse = JsonSerializer.Serialize(result),
            Timestamp = result.Timestamp
        };
    }

    // ─── Private: Poland KSeF ──────────────────────────────────────────

    private async Task<ComplianceOrchestrationResult> ProcessPolandKsefAsync(Invoice invoice, CancellationToken ct)
    {
        var result = await _polandKsefService.ReportAsync(invoice, ct);

        if (!result.Accepted)
        {
            invoice.Status = InvoiceStatus.NonCompliant;
            return new ComplianceOrchestrationResult
            {
                Success = false,
                Model = ComplianceModel.PolandKsef,
                ErrorMessage = result.ErrorMessage,
                ProviderResponse = JsonSerializer.Serialize(result)
            };
        }

        invoice.ComplianceId = result.ReferenceId;
        invoice.ComplianceResponse = JsonSerializer.Serialize(result);
        invoice.TransmittedAt = result.Timestamp;
        invoice.Status = InvoiceStatus.Transmitted;

        return new ComplianceOrchestrationResult
        {
            Success = true,
            Model = ComplianceModel.PolandKsef,
            ComplianceId = result.ReferenceId,
            ProviderResponse = JsonSerializer.Serialize(result),
            Timestamp = result.Timestamp
        };
    }

    // ─── Private: Post-Audit (manual archival) ─────────────────────────

    private async Task<ComplianceOrchestrationResult> ProcessPostAuditAsync(Invoice invoice, CancellationToken ct)
    {
        var archivalHash = await ComputeArchivalHashAsync(invoice, ct);

        invoice.ComplianceId = archivalHash;
        invoice.ComplianceResponse = JsonSerializer.Serialize(new { ArchivalHash = archivalHash });
        invoice.CompliantAt = DateTime.UtcNow;
        invoice.Status = InvoiceStatus.Compliant;

        return new ComplianceOrchestrationResult
        {
            Success = true,
            Model = ComplianceModel.PostAudit,
            ComplianceId = archivalHash,
            ArchivalHash = archivalHash,
            ProviderResponse = $"Invoice archived with hash {archivalHash}"
        };
    }

    // ─── Private: CTC Status Checks ────────────────────────────────────

    private async Task<ComplianceOrchestrationResult> CheckItalySdiStatusAsync(Invoice invoice, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(invoice.ComplianceId))
        {
            return new ComplianceOrchestrationResult
            {
                Success = false,
                Model = ComplianceModel.ItalySdi,
                ErrorMessage = "No SdI identifier available for status check."
            };
        }

        var ack = await _italySdiService.CheckStatusAsync(invoice.ComplianceId, ct);

        return new ComplianceOrchestrationResult
        {
            Success = ack.Accepted,
            Model = ComplianceModel.ItalySdi,
            ComplianceId = ack.ReferenceId ?? invoice.ComplianceId,
            ErrorMessage = ack.ErrorMessage,
            ProviderResponse = JsonSerializer.Serialize(ack),
            Timestamp = ack.ReceivedAt
        };
    }

    private async Task<ComplianceOrchestrationResult> CheckFrancePpfStatusAsync(Invoice invoice, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(invoice.ComplianceId))
        {
            return new ComplianceOrchestrationResult
            {
                Success = false,
                Model = ComplianceModel.FrancePpf,
                ErrorMessage = "No PPF reference available for status check."
            };
        }

        var ack = await _francePpfService.GetAcknowledgmentAsync(invoice.ComplianceId, ct);

        return new ComplianceOrchestrationResult
        {
            Success = ack.Accepted,
            Model = ComplianceModel.FrancePpf,
            ComplianceId = ack.ReferenceId ?? invoice.ComplianceId,
            ErrorMessage = ack.ErrorMessage,
            ProviderResponse = JsonSerializer.Serialize(ack),
            Timestamp = ack.ReceivedAt
        };
    }

    private async Task<ComplianceOrchestrationResult> CheckPolandKsefStatusAsync(Invoice invoice, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(invoice.ComplianceId))
        {
            return new ComplianceOrchestrationResult
            {
                Success = false,
                Model = ComplianceModel.PolandKsef,
                ErrorMessage = "No KSeF reference available for status check."
            };
        }

        var ack = await _polandKsefService.GetStatusAsync(invoice.ComplianceId, ct);

        return new ComplianceOrchestrationResult
        {
            Success = ack.Accepted,
            Model = ComplianceModel.PolandKsef,
            ComplianceId = ack.ReferenceId ?? invoice.ComplianceId,
            ErrorMessage = ack.ErrorMessage,
            ProviderResponse = JsonSerializer.Serialize(ack),
            Timestamp = ack.ReceivedAt
        };
    }

    // ─── Private: Helpers ──────────────────────────────────────────────

    private static ComplianceOrchestrationResult MapClearanceResult(
        Invoice invoice,
        ComplianceModel model,
        ClearanceResult result)
    {
        if (!result.Cleared)
        {
            invoice.Status = InvoiceStatus.NonCompliant;
            return new ComplianceOrchestrationResult
            {
                Success = false,
                Model = model,
                ErrorMessage = result.ErrorMessage,
                ProviderResponse = result.ProviderResponse
            };
        }

        invoice.ComplianceId = result.ClearanceId;
        invoice.ComplianceResponse = result.ProviderResponse;
        invoice.CompliantAt = result.Timestamp;
        invoice.Status = InvoiceStatus.Compliant;

        return new ComplianceOrchestrationResult
        {
            Success = true,
            Model = model,
            ComplianceId = result.ClearanceId,
            ProviderResponse = result.ProviderResponse,
            Timestamp = result.Timestamp
        };
    }

    private static ComplianceOrchestrationResult GetCachedResult(Invoice invoice)
    {
        return new ComplianceOrchestrationResult
        {
            Success = invoice.Status == InvoiceStatus.Compliant || invoice.Status == InvoiceStatus.Transmitted,
            Model = invoice.ComplianceModel,
            ComplianceId = invoice.ComplianceId,
            ProviderResponse = invoice.ComplianceResponse,
            ErrorMessage = invoice.Status == InvoiceStatus.NonCompliant
                ? "Invoice is marked as non-compliant."
                : null
        };
    }
}
