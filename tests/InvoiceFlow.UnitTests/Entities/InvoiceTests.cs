using FluentAssertions;
using InvoiceFlow.Core.Entities;
using InvoiceFlow.Core.Enums;
using InvoiceFlow.Core.Events;

namespace InvoiceFlow.UnitTests.Entities;

public class InvoiceTests
{
    [Fact]
    public void NewInvoice_HasCorrectDefaults()
    {
        var invoice = new Invoice();

        invoice.Id.Should().Be(Guid.Empty);
        invoice.DocumentType.Should().Be(DocumentType.Invoice);
        invoice.Status.Should().Be(InvoiceStatus.Draft);
        invoice.Currency.Should().Be("EUR");
        invoice.Lines.Should().BeEmpty();
        invoice.DomainEvents.Should().BeEmpty();
        invoice.InvoiceNumber.Should().BeEmpty();
        invoice.VendorName.Should().BeEmpty();
        invoice.BuyerName.Should().BeEmpty();
    }

    [Fact]
    public void NewInvoice_CreatedAtIsUtcNow()
    {
        var before = DateTime.UtcNow;
        var invoice = new Invoice();
        var after = DateTime.UtcNow;

        invoice.CreatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public void Lines_CanAddMultipleItems()
    {
        var invoice = new Invoice();
        invoice.Lines.Add(new InvoiceLine { LineNumber = 1, Description = "Item 1" });
        invoice.Lines.Add(new InvoiceLine { LineNumber = 2, Description = "Item 2" });

        invoice.Lines.Should().HaveCount(2);
        invoice.Lines[0].LineNumber.Should().Be(1);
        invoice.Lines[1].LineNumber.Should().Be(2);
    }

    [Fact]
    public void ClearDomainEvents_EmptiesList()
    {
        var invoice = new Invoice();
        invoice.DomainEvents.Add(new InvoiceReceivedEvent
        {
            InvoiceId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Source = "Email",
            FileName = "test.pdf"
        });

        invoice.DomainEvents.Should().HaveCount(1);

        invoice.ClearDomainEvents();

        invoice.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void DomainEvents_IsPrivateSet()
    {
        var invoice = new Invoice();
        var events = invoice.DomainEvents;

        // Can add to the list via the getter
        events.Add(new InvoiceReceivedEvent
        {
            InvoiceId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Source = "Email",
            FileName = "test.pdf"
        });

        invoice.DomainEvents.Should().HaveCount(1);
    }

    [Theory]
    [InlineData(DocumentType.Invoice)]
    [InlineData(DocumentType.CreditNote)]
    [InlineData(DocumentType.DebitNote)]
    [InlineData(DocumentType.PurchaseOrder)]
    [InlineData(DocumentType.DeliveryNote)]
    [InlineData(DocumentType.Reminder)]
    public void DocumentType_CanSetAllValues(DocumentType type)
    {
        var invoice = new Invoice { DocumentType = type };

        invoice.DocumentType.Should().Be(type);
    }

    [Theory]
    [InlineData(InvoiceStatus.Draft)]
    [InlineData(InvoiceStatus.Received)]
    [InlineData(InvoiceStatus.Extracting)]
    [InlineData(InvoiceStatus.Extracted)]
    [InlineData(InvoiceStatus.PendingApproval)]
    [InlineData(InvoiceStatus.Approved)]
    [InlineData(InvoiceStatus.Rejected)]
    [InlineData(InvoiceStatus.Processing)]
    [InlineData(InvoiceStatus.Compliant)]
    [InlineData(InvoiceStatus.NonCompliant)]
    [InlineData(InvoiceStatus.Transmitted)]
    [InlineData(InvoiceStatus.Failed)]
    [InlineData(InvoiceStatus.Cancelled)]
    public void Status_CanSetAllValues(InvoiceStatus status)
    {
        var invoice = new Invoice { Status = status };

        invoice.Status.Should().Be(status);
    }

    [Fact]
    public void NullableFields_CanBeNull()
    {
        var invoice = new Invoice
        {
            DueDate = null,
            VendorTaxId = null,
            VendorEmail = null,
            BuyerTaxId = null,
            ExtractionMethod = null,
            OcrConfidence = null,
            CountryCode = null,
            ComplianceModel = null,
            ComplianceId = null,
            ComplianceResponse = null,
            OriginalFileName = null,
            StoragePath = null,
            MimeType = null,
            ReferenceNumber = null,
            Notes = null,
            ErpId = null,
            ExtractedAt = null,
            CompliantAt = null,
            TransmittedAt = null
        };

        invoice.DueDate.Should().BeNull();
        invoice.VendorTaxId.Should().BeNull();
        invoice.ComplianceModel.Should().BeNull();
    }

    [Fact]
    public void FinancialFields_CanSetAndGet()
    {
        var invoice = new Invoice
        {
            Subtotal = 1000m,
            TaxAmount = 190m,
            TotalAmount = 1190m,
            DiscountAmount = 50m,
            ShippingAmount = 25m
        };

        invoice.Subtotal.Should().Be(1000m);
        invoice.TaxAmount.Should().Be(190m);
        invoice.TotalAmount.Should().Be(1190m);
        invoice.DiscountAmount.Should().Be(50m);
        invoice.ShippingAmount.Should().Be(25m);
    }
}

public class TenantTests
{
    [Fact]
    public void NewTenant_HasCorrectDefaults()
    {
        var tenant = new Tenant();

        tenant.IsActive.Should().BeTrue();
        tenant.Name.Should().BeEmpty();
        tenant.Slug.Should().BeEmpty();
    }

    [Fact]
    public void Tenant_CanSetProperties()
    {
        var tenant = new Tenant
        {
            Name = "Acme Corp",
            Slug = "acme-corp",
            TaxId = "NL123456789B01",
            Country = "NL"
        };

        tenant.Name.Should().Be("Acme Corp");
        tenant.Slug.Should().Be("acme-corp");
        tenant.Country.Should().Be("NL");
    }
}
