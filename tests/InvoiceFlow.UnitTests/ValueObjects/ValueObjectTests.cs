using FluentAssertions;
using InvoiceFlow.Core.ValueObjects;

namespace InvoiceFlow.UnitTests.ValueObjects;

public class AddressTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var address = new Address("123 Main St", "Amsterdam", "NH", "1012 AB", "NL", "Suite 100");

        address.Street.Should().Be("123 Main St");
        address.City.Should().Be("Amsterdam");
        address.State.Should().Be("NH");
        address.PostalCode.Should().Be("1012 AB");
        address.Country.Should().Be("NL");
        address.AddressLine2.Should().Be("Suite 100");
    }

    [Fact]
    public void Constructor_AddressLine2_DefaultsToNull()
    {
        var address = new Address("123 Main St", "Amsterdam", "NH", "1012 AB", "NL");

        address.AddressLine2.Should().BeNull();
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var a = new Address("123 Main St", "Amsterdam", "NH", "1012 AB", "NL");
        var b = new Address("123 Main St", "Amsterdam", "NH", "1012 AB", "NL");

        a.Should().Be(b);
    }

    [Fact]
    public void Equality_DifferentValues_AreNotEqual()
    {
        var a = new Address("123 Main St", "Amsterdam", "NH", "1012 AB", "NL");
        var b = new Address("456 Other St", "Rotterdam", "ZH", "3011 AA", "NL");

        a.Should().NotBe(b);
    }
}

public class ContactTests
{
    [Fact]
    public void Constructor_SetsRequiredName()
    {
        var contact = new Contact("John Doe");

        contact.Name.Should().Be("John Doe");
        contact.Email.Should().BeNull();
        contact.Phone.Should().BeNull();
        contact.TaxId.Should().BeNull();
    }

    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var contact = new Contact("John Doe", "john@example.com", "+31612345678", "NL123456789B01");

        contact.Name.Should().Be("John Doe");
        contact.Email.Should().Be("john@example.com");
        contact.Phone.Should().Be("+31612345678");
        contact.TaxId.Should().Be("NL123456789B01");
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var a = new Contact("John", "j@e.com");
        var b = new Contact("John", "j@e.com");

        a.Should().Be(b);
    }
}

public class TaxInfoTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var tax = new TaxInfo("NL123456789B01", "VAT", 21.0m, new Money(210m, "EUR"));

        tax.TaxId.Should().Be("NL123456789B01");
        tax.TaxType.Should().Be("VAT");
        tax.Rate.Should().Be(21.0m);
        tax.Amount.Should().Be(new Money(210m, "EUR"));
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var a = new TaxInfo("ID", "VAT", 19m, new Money(190m, "EUR"));
        var b = new TaxInfo("ID", "VAT", 19m, new Money(190m, "EUR"));

        a.Should().Be(b);
    }

    [Fact]
    public void Equality_DifferentValues_AreNotEqual()
    {
        var a = new TaxInfo("ID", "VAT", 19m, new Money(190m, "EUR"));
        var b = new TaxInfo("ID", "VAT", 21m, new Money(210m, "EUR"));

        a.Should().NotBe(b);
    }
}
