namespace InvoiceFlow.Core.ValueObjects;

/// <summary>Represents tax information for an invoice or line item.</summary>
/// <param name="TaxId">Tax identification number (e.g., VAT number).</param>
/// <param name="TaxType">Type of tax (VAT, GST, ICMS, etc.).</param>
/// <param name="Rate">Tax rate as a percentage (e.g., 19.0 for 19%).</param>
/// <param name="Amount">Tax amount in the invoice currency.</param>
public record TaxInfo(string TaxId, string TaxType, decimal Rate, Money Amount);
