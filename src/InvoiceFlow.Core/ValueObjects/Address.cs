namespace InvoiceFlow.Core.ValueObjects;

/// <summary>Represents a postal address.</summary>
/// <param name="Street">Street name and number.</param>
/// <param name="City">City or locality.</param>
/// <param name="State">State, province, or region.</param>
/// <param name="PostalCode">ZIP or postal code.</param>
/// <param name="Country">ISO 3166-1 alpha-2 country code.</param>
/// <param name="AddressLine2">Optional second address line.</param>
public record Address(
    string Street,
    string City,
    string State,
    string PostalCode,
    string Country,
    string? AddressLine2 = null);
