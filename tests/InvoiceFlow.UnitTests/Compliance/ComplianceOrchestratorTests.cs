using FluentAssertions;
using InvoiceFlow.Core.Entities;
using InvoiceFlow.Core.Enums;
using InvoiceFlow.Core.Interfaces;
using InvoiceFlow.Core.Models.Compliance;
using InvoiceFlow.Infrastructure.Compliance;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace InvoiceFlow.UnitTests.Compliance;

/// <summary>
/// Tests for ComplianceOrchestrator — routes invoices to the correct compliance handler,
/// tracks compliance status, supports post-audit archival hashing, and handles errors.
/// </summary>
public class ComplianceOrchestratorTests
{
    private readonly IPeppolComplianceService _peppolService = Substitute.For<IPeppolComplianceService>();
    private readonly IZatcaComplianceService _zatcaService = Substitute.For<IZatcaComplianceService>();
    private readonly IBrazilNfeService _brazilNfeService = Substitute.For<IBrazilNfeService>();
    private readonly IIndiaIrpService _indiaIrpService = Substitute.For<IIndiaIrpService>();
    private readonly IMexicoCfdiService _mexicoCfdiService = Substitute.For<IMexicoCfdiService>();
    private readonly IItalySdiService _italySdiService = Substitute.For<IItalySdiService>();
    private readonly IFrancePpfService _francePpfService = Substitute.For<IFrancePpfService>();
    private readonly IPolandKsefService _polandKsefService = Substitute.For<IPolandKsefService>();
    private readonly IRepository<ComplianceConfig> _configRepository = Substitute.For<IRepository<ComplianceConfig>>();
    private readonly ILogger<ComplianceOrchestrator> _logger = Substitute.For<ILogger<ComplianceOrchestrator>>();

    private ComplianceOrchestrator CreateSut()
        => new(
            _peppolService,
            _zatcaService,
            _brazilNfeService,
            _indiaIrpService,
            _mexicoCfdiService,
            _italySdiService,
            _francePpfService,
            _polandKsefService,
            _configRepository,
            _logger);

    private static Invoice CreateTestInvoice(
        ComplianceModel? model = null,
        string? countryCode = null,
        Guid? tenantId = null)
        => new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId ?? Guid.NewGuid(),
            InvoiceNumber = "INV-TEST-001",
            InvoiceDate = new DateTime(2024, 6, 15),
            VendorName = "Test Vendor GmbH",
            BuyerName = "Test Buyer Corp",
            TotalAmount = 1190.00m,
            CountryCode = countryCode,
            ComplianceModel = model,
            Status = InvoiceStatus.Processing
        };

    // ─── Routing: Peppol ───────────────────────────────────────────────

    [Fact]
    public async Task ProcessAsync_PeppolModel_CallsPeppolValidateAndTransmit()
    {
        var invoice = CreateTestInvoice(model: ComplianceModel.Peppol);
        var sut = CreateSut();

        _peppolService.ValidateAsync(invoice, Arg.Any<CancellationToken>())
            .Returns(new PeppolValidationResult { IsValid = true });
        _peppolService.GenerateUblXml(invoice)
            .Returns("<xml>ubl</xml>");
        _peppolService.TransmitAsync(invoice, "<xml>ubl</xml>", Arg.Any<CancellationToken>())
            .Returns(new PeppolTransmissionResult { Success = true, TransmissionId = "PEPPOL-123" });

        var result = await sut.ProcessAsync(invoice);

        result.Success.Should().BeTrue();
        result.Model.Should().Be(ComplianceModel.Peppol);
        result.ComplianceId.Should().Be("PEPPOL-123");
        await _peppolService.Received(1).ValidateAsync(invoice, Arg.Any<CancellationToken>());
        await _peppolService.Received(1).TransmitAsync(invoice, "<xml>ubl</xml>", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessAsync_PeppolValidationFails_ReturnsFailureAndMarksNonCompliant()
    {
        var invoice = CreateTestInvoice(model: ComplianceModel.Peppol);
        var sut = CreateSut();

        _peppolService.ValidateAsync(invoice, Arg.Any<CancellationToken>())
            .Returns(new PeppolValidationResult { IsValid = false, Errors = ["Missing buyer name"] });

        var result = await sut.ProcessAsync(invoice);

        result.Success.Should().BeFalse();
        result.Model.Should().Be(ComplianceModel.Peppol);
        result.ErrorMessage.Should().Contain("Missing buyer name");
        invoice.Status.Should().Be(InvoiceStatus.NonCompliant);

        await _peppolService.DidNotReceive().TransmitAsync(
            Arg.Any<Invoice>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // ─── Routing: Zatca ────────────────────────────────────────────────

    [Fact]
    public async Task ProcessAsync_ZatcaModel_CallsRequestClearance()
    {
        var invoice = CreateTestInvoice(model: ComplianceModel.Zatca);
        var sut = CreateSut();

        _zatcaService.RequestClearanceAsync(invoice, Arg.Any<CancellationToken>())
            .Returns(new ZatcaClearanceResult { Cleared = true, ClearanceId = "ZATCA-UUID-001" });

        var result = await sut.ProcessAsync(invoice);

        result.Success.Should().BeTrue();
        result.Model.Should().Be(ComplianceModel.Zatca);
        result.ComplianceId.Should().Be("ZATCA-UUID-001");
        invoice.ComplianceId.Should().Be("ZATCA-UUID-001");
        invoice.Status.Should().Be(InvoiceStatus.Compliant);
        invoice.CompliantAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ProcessAsync_ZatcaClearanceFails_ReturnsFailure()
    {
        var invoice = CreateTestInvoice(model: ComplianceModel.Zatca);
        var sut = CreateSut();

        _zatcaService.RequestClearanceAsync(invoice, Arg.Any<CancellationToken>())
            .Returns(new ZatcaClearanceResult { Cleared = false, ErrorMessage = "ZATCA rejection" });

        var result = await sut.ProcessAsync(invoice);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("ZATCA rejection");
        invoice.Status.Should().Be(InvoiceStatus.NonCompliant);
    }

    // ─── Routing: Brazil NF-e ──────────────────────────────────────────

    [Fact]
    public async Task ProcessAsync_BrazilNfeModel_CallsSubmitNfe()
    {
        var invoice = CreateTestInvoice(model: ComplianceModel.BrazilNfe);
        var sut = CreateSut();

        _brazilNfeService.SubmitNfeAsync(invoice, Arg.Any<CancellationToken>())
            .Returns(new ClearanceResult { Cleared = true, ClearanceId = "NFE-PROTO-001" });

        var result = await sut.ProcessAsync(invoice);

        result.Success.Should().BeTrue();
        result.Model.Should().Be(ComplianceModel.BrazilNfe);
        result.ComplianceId.Should().Be("NFE-PROTO-001");
        invoice.Status.Should().Be(InvoiceStatus.Compliant);
        await _brazilNfeService.Received(1).SubmitNfeAsync(invoice, Arg.Any<CancellationToken>());
    }

    // ─── Routing: India IRP ────────────────────────────────────────────

    [Fact]
    public async Task ProcessAsync_IndiaIrpModel_CallsSubmitEinvoice()
    {
        var invoice = CreateTestInvoice(model: ComplianceModel.IndiaIrp);
        var sut = CreateSut();

        _indiaIrpService.SubmitEinvoiceAsync(invoice, Arg.Any<CancellationToken>())
            .Returns(new ClearanceResult { Cleared = true, ClearanceId = "IRN-ABC123" });

        var result = await sut.ProcessAsync(invoice);

        result.Success.Should().BeTrue();
        result.Model.Should().Be(ComplianceModel.IndiaIrp);
        result.ComplianceId.Should().Be("IRN-ABC123");
        invoice.Status.Should().Be(InvoiceStatus.Compliant);
        await _indiaIrpService.Received(1).SubmitEinvoiceAsync(invoice, Arg.Any<CancellationToken>());
    }

    // ─── Routing: Mexico CFDI ──────────────────────────────────────────

    [Fact]
    public async Task ProcessAsync_MexicoCfdiModel_CallsStampCfdi()
    {
        var invoice = CreateTestInvoice(model: ComplianceModel.MexicoCfdi);
        var sut = CreateSut();

        _mexicoCfdiService.StampCfdiAsync(invoice, Arg.Any<CancellationToken>())
            .Returns(new ClearanceResult { Cleared = true, ClearanceId = "UUID-CFDI-001" });

        var result = await sut.ProcessAsync(invoice);

        result.Success.Should().BeTrue();
        result.Model.Should().Be(ComplianceModel.MexicoCfdi);
        result.ComplianceId.Should().Be("UUID-CFDI-001");
        invoice.Status.Should().Be(InvoiceStatus.Compliant);
        await _mexicoCfdiService.Received(1).StampCfdiAsync(invoice, Arg.Any<CancellationToken>());
    }

    // ─── Routing: Italy SdI (2-step) ───────────────────────────────────

    [Fact]
    public async Task ProcessAsync_ItalySdiModel_FormatsAndTransmits()
    {
        var invoice = CreateTestInvoice(model: ComplianceModel.ItalySdi);
        var sut = CreateSut();

        _italySdiService.FormatFatturaPaForSdi(invoice)
            .Returns("<FatturaPA>xml</FatturaPA>");
        _italySdiService.TransmitAsync(invoice, "<FatturaPA>xml</FatturaPA>", Arg.Any<CancellationToken>())
            .Returns(new ReportingResult { Accepted = true, ReferenceId = "SDI-12345" });

        var result = await sut.ProcessAsync(invoice);

        result.Success.Should().BeTrue();
        result.Model.Should().Be(ComplianceModel.ItalySdi);
        result.ComplianceId.Should().Be("SDI-12345");
        invoice.ComplianceId.Should().Be("SDI-12345");
        invoice.Status.Should().Be(InvoiceStatus.Transmitted);
        invoice.TransmittedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ProcessAsync_ItalySdiTransmitFails_ReturnsFailure()
    {
        var invoice = CreateTestInvoice(model: ComplianceModel.ItalySdi);
        var sut = CreateSut();

        _italySdiService.FormatFatturaPaForSdi(invoice)
            .Returns("<FatturaPA>xml</FatturaPA>");
        _italySdiService.TransmitAsync(invoice, "<FatturaPA>xml</FatturaPA>", Arg.Any<CancellationToken>())
            .Returns(new ReportingResult { Accepted = false, ErrorMessage = "SdI rejection" });

        var result = await sut.ProcessAsync(invoice);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("SdI rejection");
        invoice.Status.Should().Be(InvoiceStatus.NonCompliant);
    }

    // ─── Routing: France PPF ───────────────────────────────────────────

    [Fact]
    public async Task ProcessAsync_FrancePpfModel_CallsReport()
    {
        var invoice = CreateTestInvoice(model: ComplianceModel.FrancePpf);
        var sut = CreateSut();

        _francePpfService.ReportAsync(invoice, Arg.Any<CancellationToken>())
            .Returns(new ReportingResult { Accepted = true, ReferenceId = "PPF-REF-001" });

        var result = await sut.ProcessAsync(invoice);

        result.Success.Should().BeTrue();
        result.Model.Should().Be(ComplianceModel.FrancePpf);
        result.ComplianceId.Should().Be("PPF-REF-001");
        invoice.Status.Should().Be(InvoiceStatus.Transmitted);
        invoice.TransmittedAt.Should().NotBeNull();
    }

    // ─── Routing: Poland KSeF ──────────────────────────────────────────

    [Fact]
    public async Task ProcessAsync_PolandKsefModel_CallsReport()
    {
        var invoice = CreateTestInvoice(model: ComplianceModel.PolandKsef);
        var sut = CreateSut();

        _polandKsefService.ReportAsync(invoice, Arg.Any<CancellationToken>())
            .Returns(new ReportingResult { Accepted = true, ReferenceId = "KSEF-REF-001" });

        var result = await sut.ProcessAsync(invoice);

        result.Success.Should().BeTrue();
        result.Model.Should().Be(ComplianceModel.PolandKsef);
        result.ComplianceId.Should().Be("KSEF-REF-001");
        invoice.Status.Should().Be(InvoiceStatus.Transmitted);
    }

    // ─── Routing: PostAudit ────────────────────────────────────────────

    [Fact]
    public async Task ProcessAsync_PostAuditModel_ComputesHashAndMarksCompliant()
    {
        var invoice = CreateTestInvoice(model: ComplianceModel.PostAudit);
        var sut = CreateSut();

        var result = await sut.ProcessAsync(invoice);

        result.Success.Should().BeTrue();
        result.Model.Should().Be(ComplianceModel.PostAudit);
        result.ArchivalHash.Should().NotBeNullOrEmpty();
        result.ArchivalHash.Should().HaveLength(64); // SHA-256 hex = 64 chars
        invoice.ComplianceId.Should().Be(result.ArchivalHash);
        invoice.Status.Should().Be(InvoiceStatus.Compliant);
        invoice.CompliantAt.Should().NotBeNull();

        // No compliance service should have been called
        await _peppolService.DidNotReceive().ValidateAsync(Arg.Any<Invoice>(), Arg.Any<CancellationToken>());
        await _zatcaService.DidNotReceive().RequestClearanceAsync(Arg.Any<Invoice>(), Arg.Any<CancellationToken>());
    }

    // ─── Country Inference ─────────────────────────────────────────────

    [Theory]
    [InlineData("SA", ComplianceModel.Zatca)]
    [InlineData("sa", ComplianceModel.Zatca)]
    [InlineData("BR", ComplianceModel.BrazilNfe)]
    [InlineData("IN", ComplianceModel.IndiaIrp)]
    [InlineData("MX", ComplianceModel.MexicoCfdi)]
    [InlineData("IT", ComplianceModel.ItalySdi)]
    [InlineData("FR", ComplianceModel.FrancePpf)]
    [InlineData("PL", ComplianceModel.PolandKsef)]
    [InlineData("DE", ComplianceModel.Peppol)]
    [InlineData("NL", ComplianceModel.Peppol)]
    [InlineData(null, ComplianceModel.Peppol)]
    public async Task ProcessAsync_NoExplicitModel_InfersModelFromCountryCode(
        string? countryCode, ComplianceModel expectedModel)
    {
        var invoice = CreateTestInvoice(model: null, countryCode: countryCode);
        var sut = CreateSut();

        _configRepository.GetAllAsync(0, 1000, Arg.Any<CancellationToken>())
            .Returns([]);

        // Set up minimal mock returns for each model
        SetupMockForModel(expectedModel, invoice);

        await sut.ProcessAsync(invoice);

        invoice.ComplianceModel.Should().Be(expectedModel);
    }

    // ─── Config Lookup ─────────────────────────────────────────────────

    [Fact]
    public async Task ProcessAsync_ConfigExists_UsesConfigModel()
    {
        var tenantId = Guid.NewGuid();
        var invoice = CreateTestInvoice(model: null, countryCode: "DE", tenantId: tenantId);
        var sut = CreateSut();

        _configRepository.GetAllAsync(0, 1000, Arg.Any<CancellationToken>())
            .Returns([
                new ComplianceConfig
                {
                    TenantId = tenantId,
                    CountryCode = "DE",
                    Model = ComplianceModel.ItalySdi,
                    IsEnabled = true
                }
            ]);

        _italySdiService.FormatFatturaPaForSdi(invoice)
            .Returns("<FatturaPA/>");
        _italySdiService.TransmitAsync(invoice, "<FatturaPA/>", Arg.Any<CancellationToken>())
            .Returns(new ReportingResult { Accepted = true, ReferenceId = "SDI-CONFIG" });

        await sut.ProcessAsync(invoice);

        invoice.ComplianceModel.Should().Be(ComplianceModel.ItalySdi);
    }

    // ─── Invoice Entity Updates on Success ─────────────────────────────

    [Fact]
    public async Task ProcessAsync_ZatcaSuccess_UpdatesInvoiceFields()
    {
        var invoice = CreateTestInvoice(model: ComplianceModel.Zatca);
        var sut = CreateSut();

        var clearanceResult = new ZatcaClearanceResult
        {
            Cleared = true,
            ClearanceId = "UUID-UPDATE-TEST",
            InvoiceHash = "hash123",
            Timestamp = new DateTime(2024, 6, 15, 12, 0, 0, DateTimeKind.Utc)
        };

        _zatcaService.RequestClearanceAsync(invoice, Arg.Any<CancellationToken>())
            .Returns(clearanceResult);

        await sut.ProcessAsync(invoice);

        invoice.ComplianceId.Should().Be("UUID-UPDATE-TEST");
        invoice.ComplianceResponse.Should().Contain("UUID-UPDATE-TEST");
        invoice.CompliantAt.Should().Be(clearanceResult.Timestamp);
        invoice.Status.Should().Be(InvoiceStatus.Compliant);
    }

    // ─── Failure Handling ──────────────────────────────────────────────

    [Fact]
    public async Task ProcessAsync_ServiceThrowsException_ReturnsErrorAndMarksNonCompliant()
    {
        var invoice = CreateTestInvoice(model: ComplianceModel.Zatca);
        var sut = CreateSut();

        _zatcaService.RequestClearanceAsync(invoice, Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("ZATCA API unavailable"));

        var result = await sut.ProcessAsync(invoice);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("ZATCA API unavailable");
        result.Model.Should().Be(ComplianceModel.Zatca);
        invoice.Status.Should().Be(InvoiceStatus.NonCompliant);
    }

    // ─── ComputeArchivalHash ───────────────────────────────────────────

    [Fact]
    public async Task ComputeArchivalHashAsync_ReturnsConsistentSha256Hash()
    {
        var invoice = CreateTestInvoice(model: ComplianceModel.PostAudit);
        invoice.InvoiceNumber = "INV-HASH-001";
        invoice.VendorName = "Hash Vendor";
        invoice.BuyerName = "Hash Buyer";
        invoice.TotalAmount = 2500.00m;
        invoice.InvoiceDate = new DateTime(2024, 1, 15);

        var sut = CreateSut();

        var hash1 = await sut.ComputeArchivalHashAsync(invoice);
        var hash2 = await sut.ComputeArchivalHashAsync(invoice);

        hash1.Should().Be(hash2);
        hash1.Should().HaveLength(64);
        hash1.Should().MatchRegex("^[0-9a-f]{64}$");
    }

    [Fact]
    public async Task ComputeArchivalHashAsync_DifferentInvoices_DifferentHashes()
    {
        var sut = CreateSut();

        var invoice1 = CreateTestInvoice(model: ComplianceModel.PostAudit);
        invoice1.InvoiceNumber = "INV-001";
        invoice1.TotalAmount = 100.00m;

        var invoice2 = CreateTestInvoice(model: ComplianceModel.PostAudit);
        invoice2.InvoiceNumber = "INV-002";
        invoice2.TotalAmount = 200.00m;

        var hash1 = await sut.ComputeArchivalHashAsync(invoice1);
        var hash2 = await sut.ComputeArchivalHashAsync(invoice2);

        hash1.Should().NotBe(hash2);
    }

    // ─── CheckStatus: CTC Models ───────────────────────────────────────

    [Fact]
    public async Task CheckStatusAsync_ItalySdi_CallsCheckStatusWithIdentifier()
    {
        var invoice = CreateTestInvoice(model: ComplianceModel.ItalySdi);
        invoice.ComplianceId = "SDI-CHECK-123";
        var sut = CreateSut();

        _italySdiService.CheckStatusAsync("SDI-CHECK-123", Arg.Any<CancellationToken>())
            .Returns(new ReportingAcknowledgment { Accepted = true, ReferenceId = "SDI-CHECK-123" });

        var result = await sut.CheckStatusAsync(invoice);

        result.Success.Should().BeTrue();
        result.Model.Should().Be(ComplianceModel.ItalySdi);
        await _italySdiService.Received(1).CheckStatusAsync("SDI-CHECK-123", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckStatusAsync_ItalySdi_NoComplianceId_ReturnsError()
    {
        var invoice = CreateTestInvoice(model: ComplianceModel.ItalySdi);
        invoice.ComplianceId = null;
        var sut = CreateSut();

        var result = await sut.CheckStatusAsync(invoice);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("No SdI identifier available");
    }

    [Fact]
    public async Task CheckStatusAsync_FrancePpf_CallsGetAcknowledgment()
    {
        var invoice = CreateTestInvoice(model: ComplianceModel.FrancePpf);
        invoice.ComplianceId = "PPF-CHECK-456";
        var sut = CreateSut();

        _francePpfService.GetAcknowledgmentAsync("PPF-CHECK-456", Arg.Any<CancellationToken>())
            .Returns(new ReportingAcknowledgment { Accepted = true, ReferenceId = "PPF-CHECK-456" });

        var result = await sut.CheckStatusAsync(invoice);

        result.Success.Should().BeTrue();
        result.Model.Should().Be(ComplianceModel.FrancePpf);
        await _francePpfService.Received(1).GetAcknowledgmentAsync("PPF-CHECK-456", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckStatusAsync_PolandKsef_CallsGetStatus()
    {
        var invoice = CreateTestInvoice(model: ComplianceModel.PolandKsef);
        invoice.ComplianceId = "KSEF-CHECK-789";
        var sut = CreateSut();

        _polandKsefService.GetStatusAsync("KSEF-CHECK-789", Arg.Any<CancellationToken>())
            .Returns(new ReportingAcknowledgment { Accepted = false, ErrorCode = "PENDING", ErrorMessage = "Processing" });

        var result = await sut.CheckStatusAsync(invoice);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Processing");
        await _polandKsefService.Received(1).GetStatusAsync("KSEF-CHECK-789", Arg.Any<CancellationToken>());
    }

    // ─── CheckStatus: Clearance Models (cached) ────────────────────────

    [Fact]
    public async Task CheckStatusAsync_Zatca_ReturnsCachedResult()
    {
        var invoice = CreateTestInvoice(model: ComplianceModel.Zatca);
        invoice.ComplianceId = "ZATCA-CACHED";
        invoice.Status = InvoiceStatus.Compliant;
        invoice.ComplianceResponse = "{\"cached\":true}";
        var sut = CreateSut();

        var result = await sut.CheckStatusAsync(invoice);

        result.Success.Should().BeTrue();
        result.Model.Should().Be(ComplianceModel.Zatca);
        result.ComplianceId.Should().Be("ZATCA-CACHED");
        result.ProviderResponse.Should().Contain("cached");

        // No Zatca service call should be made for non-CTC models
        await _zatcaService.DidNotReceive().RequestClearanceAsync(Arg.Any<Invoice>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckStatusAsync_NonCompliantInvoice_ReturnsCachedFailure()
    {
        var invoice = CreateTestInvoice(model: ComplianceModel.Zatca);
        invoice.Status = InvoiceStatus.NonCompliant;
        var sut = CreateSut();

        var result = await sut.CheckStatusAsync(invoice);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("non-compliant");
    }

    // ─── CheckStatus: No Model Assigned ────────────────────────────────

    [Fact]
    public async Task CheckStatusAsync_NoModelAssigned_ReturnsError()
    {
        var invoice = CreateTestInvoice(model: null);
        var sut = CreateSut();

        var result = await sut.CheckStatusAsync(invoice);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("no compliance model assigned");
    }

    // ─── Null Guards ───────────────────────────────────────────────────

    [Fact]
    public async Task ProcessAsync_NullInvoice_ThrowsArgumentNullException()
    {
        var sut = CreateSut();

        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.ProcessAsync(null!));
    }

    [Fact]
    public async Task CheckStatusAsync_NullInvoice_ThrowsArgumentNullException()
    {
        var sut = CreateSut();

        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.CheckStatusAsync(null!));
    }

    [Fact]
    public async Task ComputeArchivalHashAsync_NullInvoice_ThrowsArgumentNullException()
    {
        var sut = CreateSut();

        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.ComputeArchivalHashAsync(null!));
    }

    // ─── Country Inference Default ─────────────────────────────────────

    [Fact]
    public async Task ProcessAsync_UnknownCountryCode_DefaultsToPeppol()
    {
        var invoice = CreateTestInvoice(model: null, countryCode: "US");
        var sut = CreateSut();

        _configRepository.GetAllAsync(0, 1000, Arg.Any<CancellationToken>())
            .Returns([]);

        _peppolService.ValidateAsync(invoice, Arg.Any<CancellationToken>())
            .Returns(new PeppolValidationResult { IsValid = true });
        _peppolService.GenerateUblXml(invoice)
            .Returns("<xml/>");
        _peppolService.TransmitAsync(invoice, "<xml/>", Arg.Any<CancellationToken>())
            .Returns(new PeppolTransmissionResult { Success = true, TransmissionId = "PEPPOL-US" });

        await sut.ProcessAsync(invoice);

        invoice.ComplianceModel.Should().Be(ComplianceModel.Peppol);
        await _peppolService.Received(1).ValidateAsync(invoice, Arg.Any<CancellationToken>());
    }

    // ─── Helper ────────────────────────────────────────────────────────

    private void SetupMockForModel(ComplianceModel model, Invoice invoice)
    {
        switch (model)
        {
            case ComplianceModel.Peppol:
                _peppolService.ValidateAsync(invoice, Arg.Any<CancellationToken>())
                    .Returns(new PeppolValidationResult { IsValid = true });
                _peppolService.GenerateUblXml(invoice).Returns("<xml/>");
                _peppolService.TransmitAsync(invoice, "<xml/>", Arg.Any<CancellationToken>())
                    .Returns(new PeppolTransmissionResult { Success = true, TransmissionId = "PP" });
                break;
            case ComplianceModel.Zatca:
                _zatcaService.RequestClearanceAsync(invoice, Arg.Any<CancellationToken>())
                    .Returns(new ZatcaClearanceResult { Cleared = true, ClearanceId = "ZC" });
                break;
            case ComplianceModel.BrazilNfe:
                _brazilNfeService.SubmitNfeAsync(invoice, Arg.Any<CancellationToken>())
                    .Returns(new ClearanceResult { Cleared = true, ClearanceId = "BN" });
                break;
            case ComplianceModel.IndiaIrp:
                _indiaIrpService.SubmitEinvoiceAsync(invoice, Arg.Any<CancellationToken>())
                    .Returns(new ClearanceResult { Cleared = true, ClearanceId = "II" });
                break;
            case ComplianceModel.MexicoCfdi:
                _mexicoCfdiService.StampCfdiAsync(invoice, Arg.Any<CancellationToken>())
                    .Returns(new ClearanceResult { Cleared = true, ClearanceId = "MC" });
                break;
            case ComplianceModel.ItalySdi:
                _italySdiService.FormatFatturaPaForSdi(invoice).Returns("<xml/>");
                _italySdiService.TransmitAsync(invoice, "<xml/>", Arg.Any<CancellationToken>())
                    .Returns(new ReportingResult { Accepted = true, ReferenceId = "IS" });
                break;
            case ComplianceModel.FrancePpf:
                _francePpfService.ReportAsync(invoice, Arg.Any<CancellationToken>())
                    .Returns(new ReportingResult { Accepted = true, ReferenceId = "FP" });
                break;
            case ComplianceModel.PolandKsef:
                _polandKsefService.ReportAsync(invoice, Arg.Any<CancellationToken>())
                    .Returns(new ReportingResult { Accepted = true, ReferenceId = "PK" });
                break;
        }
    }
}
