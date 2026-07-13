using FluentAssertions;
using InvoiceFlow.Core.Entities;

namespace InvoiceFlow.UnitTests.Entities;

public class InvoiceLineTests
{
    [Fact]
    public void NewLine_HasCorrectDefaults()
    {
        var line = new InvoiceLine();

        line.Id.Should().Be(Guid.Empty);
        line.InvoiceId.Should().Be(Guid.Empty);
        line.LineNumber.Should().Be(0);
        line.Description.Should().BeEmpty();
        line.Quantity.Should().Be(0m);
        line.UnitPrice.Should().Be(0m);
        line.LineTotal.Should().Be(0m);
        line.TaxRate.Should().Be(0m);
        line.TaxAmount.Should().Be(0m);
    }

    [Fact]
    public void Line_CanSetAllProperties()
    {
        var invoiceId = Guid.NewGuid();
        var line = new InvoiceLine
        {
            InvoiceId = invoiceId,
            LineNumber = 1,
            Description = "Widget A",
            ProductCode = "WA-001",
            HsnCode = "8471",
            Quantity = 10,
            Unit = "EA",
            UnitPrice = 25.50m,
            LineTotal = 255.00m,
            TaxRate = 19.0m,
            TaxAmount = 48.45m,
            TaxCategory = "S",
            DiscountPercent = 5m,
            DiscountAmount = 12.75m
        };

        line.InvoiceId.Should().Be(invoiceId);
        line.LineNumber.Should().Be(1);
        line.Description.Should().Be("Widget A");
        line.ProductCode.Should().Be("WA-001");
        line.HsnCode.Should().Be("8471");
        line.Quantity.Should().Be(10m);
        line.Unit.Should().Be("EA");
        line.UnitPrice.Should().Be(25.50m);
        line.LineTotal.Should().Be(255.00m);
        line.TaxRate.Should().Be(19.0m);
        line.TaxAmount.Should().Be(48.45m);
        line.TaxCategory.Should().Be("S");
        line.DiscountPercent.Should().Be(5m);
        line.DiscountAmount.Should().Be(12.75m);
    }

    [Fact]
    public void OptionalFields_DefaultToNull()
    {
        var line = new InvoiceLine();

        line.ProductCode.Should().BeNull();
        line.HsnCode.Should().BeNull();
        line.Unit.Should().BeNull();
        line.TaxCategory.Should().BeNull();
        line.DiscountPercent.Should().BeNull();
        line.DiscountAmount.Should().BeNull();
    }
}
