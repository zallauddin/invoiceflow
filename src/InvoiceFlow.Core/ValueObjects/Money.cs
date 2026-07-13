namespace InvoiceFlow.Core.ValueObjects;

/// <summary>Represents a monetary amount with its currency.</summary>
public record Money
{
    public decimal Amount { get; }
    public string Currency { get; }

    /// <summary>Zero amount with specified currency (default EUR).</summary>
    public static Money Zero(string currency = "EUR") => new(0m, currency);

    public Money(decimal amount, string currency)
    {
        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
            throw new ArgumentException("Currency must be a 3-letter ISO 4217 code.", nameof(currency));
        Amount = amount;
        Currency = currency.ToUpperInvariant();
    }

    public static Money operator +(Money left, Money right)
    {
        if (left.Currency != right.Currency)
            throw new InvalidOperationException($"Cannot add different currencies: {left.Currency} and {right.Currency}");
        return new Money(left.Amount + right.Amount, left.Currency);
    }

    public static Money operator -(Money left, Money right)
    {
        if (left.Currency != right.Currency)
            throw new InvalidOperationException($"Cannot subtract different currencies: {left.Currency} and {right.Currency}");
        return new Money(left.Amount - right.Amount, left.Currency);
    }

    public static bool operator >(Money left, Money right) => left.Amount > right.Amount;
    public static bool operator <(Money left, Money right) => left.Amount < right.Amount;
    public static bool operator >=(Money left, Money right) => left.Amount >= right.Amount;
    public static bool operator <=(Money left, Money right) => left.Amount <= right.Amount;

    public override string ToString() => $"{Amount:G} {Currency}";

    public static bool TryParse(string? value, out Money? result)
    {
        result = null;
        if (string.IsNullOrWhiteSpace(value)) return false;
        var parts = value.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2) return false;
        if (!decimal.TryParse(parts[0], out var amount)) return false;
        result = new Money(amount, parts[1]);
        return true;
    }
}
