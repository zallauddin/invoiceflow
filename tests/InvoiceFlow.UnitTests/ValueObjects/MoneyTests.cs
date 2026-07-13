using FluentAssertions;
using InvoiceFlow.Core.ValueObjects;

namespace InvoiceFlow.UnitTests.ValueObjects;

public class MoneyTests
{
    [Fact]
    public void Constructor_SetsAmountAndCurrency()
    {
        var money = new Money(100.50m, "EUR");

        money.Amount.Should().Be(100.50m);
        money.Currency.Should().Be("EUR");
    }

    [Fact]
    public void Constructor_NormalizesCurrencyToUpper()
    {
        var money = new Money(10m, "usd");

        money.Currency.Should().Be("USD");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("US")]
    [InlineData("USDX")]
    public void Constructor_InvalidCurrency_ThrowsArgumentException(string? currency)
    {
        Action act = () => new Money(10m, currency!);

        act.Should().Throw<ArgumentException>()
            .Which.Message.Should().Contain("Currency must be a 3-letter ISO 4217 code");
    }

    [Fact]
    public void Zero_DefaultCurrencyIsEUR()
    {
        var zero = Money.Zero();

        zero.Amount.Should().Be(0m);
        zero.Currency.Should().Be("EUR");
    }

    [Fact]
    public void Zero_SpecifiedCurrency()
    {
        var zero = Money.Zero("USD");

        zero.Currency.Should().Be("USD");
    }

    [Fact]
    public void Add_SameCurrency_ReturnsSum()
    {
        var left = new Money(100m, "EUR");
        var right = new Money(50m, "EUR");

        var result = left + right;

        result.Amount.Should().Be(150m);
        result.Currency.Should().Be("EUR");
    }

    [Fact]
    public void Add_DifferentCurrency_ThrowsInvalidOperation()
    {
        var left = new Money(100m, "EUR");
        var right = new Money(50m, "USD");

        Action act = () => _ = left + right;

        act.Should().Throw<InvalidOperationException>()
            .Which.Message.Should().Contain("Cannot add different currencies");
    }

    [Fact]
    public void Subtract_SameCurrency_ReturnsDifference()
    {
        var left = new Money(100m, "EUR");
        var right = new Money(30m, "EUR");

        var result = left - right;

        result.Amount.Should().Be(70m);
        result.Currency.Should().Be("EUR");
    }

    [Fact]
    public void Subtract_DifferentCurrency_ThrowsInvalidOperation()
    {
        var left = new Money(100m, "EUR");
        var right = new Money(30m, "GBP");

        Action act = () => _ = left - right;

        act.Should().Throw<InvalidOperationException>()
            .Which.Message.Should().Contain("Cannot subtract different currencies");
    }

    [Theory]
    [InlineData(100, 50, true)]
    [InlineData(50, 100, false)]
    [InlineData(50, 50, false)]
    public void GreaterThan_ReturnsExpectedResult(decimal leftAmt, decimal rightAmt, bool expected)
    {
        var left = new Money(leftAmt, "EUR");
        var right = new Money(rightAmt, "EUR");

        (left > right).Should().Be(expected);
    }

    [Theory]
    [InlineData(50, 100, true)]
    [InlineData(100, 50, false)]
    [InlineData(50, 50, false)]
    public void LessThan_ReturnsExpectedResult(decimal leftAmt, decimal rightAmt, bool expected)
    {
        var left = new Money(leftAmt, "EUR");
        var right = new Money(rightAmt, "EUR");

        (left < right).Should().Be(expected);
    }

    [Fact]
    public void GreaterThanOrEqual_EqualAmounts_ReturnsTrue()
    {
        var left = new Money(50m, "EUR");
        var right = new Money(50m, "EUR");

        (left >= right).Should().BeTrue();
    }

    [Fact]
    public void LessThanOrEqual_EqualAmounts_ReturnsTrue()
    {
        var left = new Money(50m, "EUR");
        var right = new Money(50m, "EUR");

        (left <= right).Should().BeTrue();
    }

    [Fact]
    public void ToString_FormatsCorrectly()
    {
        var money = new Money(1234.56m, "USD");

        money.ToString().Should().Be("1234.56 USD");
    }

    [Fact]
    public void TryParse_ValidInput_ReturnsTrue()
    {
        var result = Money.TryParse("100.50 EUR", out var money);

        result.Should().BeTrue();
        money.Should().NotBeNull();
        money!.Amount.Should().Be(100.50m);
        money.Currency.Should().Be("EUR");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("100")]
    [InlineData("abc EUR")]
    [InlineData("100.50")]
    public void TryParse_InvalidInput_ReturnsFalse(string? input)
    {
        var result = Money.TryParse(input, out var money);

        result.Should().BeFalse();
        money.Should().BeNull();
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var left = new Money(100m, "EUR");
        var right = new Money(100m, "EUR");

        left.Should().Be(right);
        left.GetHashCode().Should().Be(right.GetHashCode());
    }

    [Fact]
    public void Equality_DifferentValues_AreNotEqual()
    {
        var left = new Money(100m, "EUR");
        var right = new Money(200m, "EUR");

        left.Should().NotBe(right);
    }
}
