namespace InvoiceFlow.Core.ValueObjects;

/// <summary>Represents a contact person associated with an invoice.</summary>
/// <param name="Name">Full name of the contact.</param>
/// <param name="Email">Optional email address.</param>
/// <param name="Phone">Optional phone number.</param>
/// <param name="TaxId">Optional tax identification number.</param>
public record Contact(string Name, string? Email = null, string? Phone = null, string? TaxId = null);
