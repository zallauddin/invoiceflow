using System.Globalization;
using System.Xml;
using System.Xml.Serialization;
using InvoiceFlow.Core.Entities;
using InvoiceFlow.Core.Enums;
using InvoiceFlow.Formats.Abstractions;
using InvoiceFlow.Infrastructure.Formats.Ubl21.Models;

namespace InvoiceFlow.Infrastructure.Formats.Ubl21;

/// <summary>
/// Writes core Invoice + InvoiceLine entities into a UBL 2.1 XML stream.
/// Generates EN 16931-compliant UBL with Peppol BIS Billing 3.0 profile.
/// </summary>
public sealed class Ubl21FormatWriter : IFormatWriter
{
    /// <inheritdoc />
    public InvoiceFormat SupportedFormat => InvoiceFormat.Ubl21;

    /// <summary>EN 16931 customization identifier.</summary>
    private const string En16931CustomizationId = "urn:cen.eu:en16931:2017";

    /// <summary>Peppol Billing 01:1.0 profile identifier.</summary>
    private const string PeppolProfileId = "urn:fdc:peppol.eu:2017:poacc:billing:01:1.0";

    /// <summary>Write invoice data to a UBL 2.1 XML stream.</summary>
    public Task<FormatWriteResult> WriteAsync(Invoice invoice, List<InvoiceLine> lines, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        ArgumentNullException.ThrowIfNull(lines);

        var stream = new MemoryStream();

        var isCreditNote = invoice.DocumentType == DocumentType.CreditNote;
        var validationResults = new List<ValidationResult>();

        if (isCreditNote)
        {
            var creditNote = MapToCreditNote(invoice, lines);
            SerializeToStream(stream, creditNote, UblNamespaces.CreditNote);
        }
        else
        {
            var ublInvoice = MapToInvoice(invoice, lines);
            SerializeToStream(stream, ublInvoice, UblNamespaces.Invoice);
        }

        stream.Position = 0;

        var fileName = isCreditNote
            ? $"credit-note-{invoice.InvoiceNumber}.xml"
            : $"invoice-{invoice.InvoiceNumber}.xml";

        return Task.FromResult(new FormatWriteResult(
            stream,
            "application/xml",
            fileName,
            validationResults));
    }

    private static UblInvoice MapToInvoice(Invoice invoice, List<InvoiceLine> lines)
    {
        var currency = invoice.Currency ?? "EUR";

        var ublInvoice = new UblInvoice
        {
            UblVersionId = "2.1",
            CustomizationId = En16931CustomizationId,
            ProfileId = PeppolProfileId,
            Id = invoice.InvoiceNumber,
            IssueDate = invoice.InvoiceDate,
            DueDate = invoice.DueDate,
            InvoiceTypeCode = "380", // Standard invoice
            DocumentCurrencyCode = currency,
            BuyerReference = invoice.ReferenceNumber,
        };

        // Supplier (vendor)
        ublInvoice.AccountingSupplierParty = new UblSupplierParty
        {
            Party = new UblParty
            {
                PartyName = new UblPartyName { Name = invoice.VendorName },
                PartyTaxScheme = string.IsNullOrEmpty(invoice.VendorTaxId)
                    ? null
                    : new UblPartyTaxScheme
                    {
                        CompanyId = invoice.VendorTaxId,
                        TaxScheme = new UblTaxScheme { Id = "VAT" }
                    },
                PartyLegalEntity = new UblPartyLegalEntity
                {
                    RegistrationName = invoice.VendorName,
                    CompanyId = invoice.VendorTaxId,
                },
            }
        };

        // Buyer (customer)
        ublInvoice.AccountingCustomerParty = new UblCustomerParty
        {
            Party = new UblParty
            {
                PartyName = new UblPartyName { Name = invoice.BuyerName },
                PartyTaxScheme = string.IsNullOrEmpty(invoice.BuyerTaxId)
                    ? null
                    : new UblPartyTaxScheme
                    {
                        CompanyId = invoice.BuyerTaxId,
                        TaxScheme = new UblTaxScheme { Id = "VAT" }
                    },
                PartyLegalEntity = new UblPartyLegalEntity
                {
                    RegistrationName = invoice.BuyerName,
                    CompanyId = invoice.BuyerTaxId,
                },
            }
        };

        // Map lines
        ublInvoice.InvoiceLines = new List<UblInvoiceLine>(lines.Count);
        foreach (var line in lines)
        {
            ublInvoice.InvoiceLines.Add(MapLine(line, currency));
        }

        // Tax totals — group by TaxCategory
        var taxGroups = lines
            .Where(l => l.TaxRate > 0)
            .GroupBy(l => l.TaxCategory ?? "S")
            .ToList();

        if (taxGroups.Count > 0 || invoice.TaxAmount > 0)
        {
            var taxTotal = new UblTaxTotal
            {
                TaxAmount = new UblAmountType { Value = invoice.TaxAmount, CurrencyId = currency }
            };

            foreach (var group in taxGroups)
            {
                taxTotal.TaxSubtotals.Add(new UblTaxSubtotal
                {
                    TaxableAmount = new UblAmountType
                    {
                        Value = group.Sum(l => l.LineTotal),
                        CurrencyId = currency
                    },
                    TaxAmount = new UblAmountType
                    {
                        Value = group.Sum(l => l.TaxAmount),
                        CurrencyId = currency
                    },
                    TaxCategory = new UblTaxCategory
                    {
                        Id = group.Key,
                        Percent = group.First().TaxRate,
                        TaxScheme = new UblTaxScheme { Id = "VAT" }
                    }
                });
            }

            ublInvoice.TaxTotals.Add(taxTotal);
        }

        // Legal monetary total
        var lineExtension = lines.Sum(l => l.LineTotal);
        var taxExclusive = invoice.Subtotal > 0 ? invoice.Subtotal : lineExtension;
        var taxInclusive = invoice.TotalAmount > 0 ? invoice.TotalAmount : taxExclusive + invoice.TaxAmount;

        ublInvoice.LegalMonetaryTotal = new UblMonetaryTotal
        {
            LineExtensionAmount = new UblAmountType { Value = lineExtension, CurrencyId = currency },
            TaxExclusiveAmount = new UblAmountType { Value = taxExclusive, CurrencyId = currency },
            TaxInclusiveAmount = new UblAmountType { Value = taxInclusive, CurrencyId = currency },
            PayableAmount = new UblAmountType { Value = taxInclusive, CurrencyId = currency },
        };

        if (invoice.DiscountAmount is > 0)
        {
            ublInvoice.LegalMonetaryTotal.AllowanceTotalAmount = new UblAmountType
            {
                Value = invoice.DiscountAmount.Value,
                CurrencyId = currency
            };
        }

        if (invoice.ShippingAmount is > 0)
        {
            ublInvoice.LegalMonetaryTotal.ChargeTotalAmount = new UblAmountType
            {
                Value = invoice.ShippingAmount.Value,
                CurrencyId = currency
            };
        }

        return ublInvoice;
    }

    private static UblCreditNote MapToCreditNote(Invoice invoice, List<InvoiceLine> lines)
    {
        var currency = invoice.Currency ?? "EUR";

        var creditNote = new UblCreditNote
        {
            UblVersionId = "2.1",
            CustomizationId = En16931CustomizationId,
            ProfileId = PeppolProfileId,
            Id = invoice.InvoiceNumber,
            IssueDate = invoice.InvoiceDate,
            DueDate = invoice.DueDate,
            CreditNoteTypeCode = "381",
            DocumentCurrencyCode = currency,
            BuyerReference = invoice.ReferenceNumber,
        };

        // Supplier
        creditNote.AccountingSupplierParty = new UblSupplierParty
        {
            Party = new UblParty
            {
                PartyName = new UblPartyName { Name = invoice.VendorName },
                PartyTaxScheme = string.IsNullOrEmpty(invoice.VendorTaxId)
                    ? null
                    : new UblPartyTaxScheme
                    {
                        CompanyId = invoice.VendorTaxId,
                        TaxScheme = new UblTaxScheme { Id = "VAT" }
                    },
                PartyLegalEntity = new UblPartyLegalEntity
                {
                    RegistrationName = invoice.VendorName,
                    CompanyId = invoice.VendorTaxId,
                },
            }
        };

        // Buyer
        creditNote.AccountingCustomerParty = new UblCustomerParty
        {
            Party = new UblParty
            {
                PartyName = new UblPartyName { Name = invoice.BuyerName },
                PartyTaxScheme = string.IsNullOrEmpty(invoice.BuyerTaxId)
                    ? null
                    : new UblPartyTaxScheme
                    {
                        CompanyId = invoice.BuyerTaxId,
                        TaxScheme = new UblTaxScheme { Id = "VAT" }
                    },
                PartyLegalEntity = new UblPartyLegalEntity
                {
                    RegistrationName = invoice.BuyerName,
                    CompanyId = invoice.BuyerTaxId,
                },
            }
        };

        // Lines
        creditNote.CreditNoteLines = new List<UblInvoiceLine>(lines.Count);
        foreach (var line in lines)
        {
            creditNote.CreditNoteLines.Add(MapLine(line, currency));
        }

        // Tax totals
        var taxGroups = lines
            .Where(l => l.TaxRate > 0)
            .GroupBy(l => l.TaxCategory ?? "S")
            .ToList();

        if (taxGroups.Count > 0 || invoice.TaxAmount > 0)
        {
            var taxTotal = new UblTaxTotal
            {
                TaxAmount = new UblAmountType { Value = invoice.TaxAmount, CurrencyId = currency }
            };

            foreach (var group in taxGroups)
            {
                taxTotal.TaxSubtotals.Add(new UblTaxSubtotal
                {
                    TaxableAmount = new UblAmountType
                    {
                        Value = group.Sum(l => l.LineTotal),
                        CurrencyId = currency
                    },
                    TaxAmount = new UblAmountType
                    {
                        Value = group.Sum(l => l.TaxAmount),
                        CurrencyId = currency
                    },
                    TaxCategory = new UblTaxCategory
                    {
                        Id = group.Key,
                        Percent = group.First().TaxRate,
                        TaxScheme = new UblTaxScheme { Id = "VAT" }
                    }
                });
            }

            creditNote.TaxTotals.Add(taxTotal);
        }

        // Monetary total
        var lineExtension = lines.Sum(l => l.LineTotal);
        var taxExclusive = invoice.Subtotal > 0 ? invoice.Subtotal : lineExtension;
        var taxInclusive = invoice.TotalAmount > 0 ? invoice.TotalAmount : taxExclusive + invoice.TaxAmount;

        creditNote.LegalMonetaryTotal = new UblMonetaryTotal
        {
            LineExtensionAmount = new UblAmountType { Value = lineExtension, CurrencyId = currency },
            TaxExclusiveAmount = new UblAmountType { Value = taxExclusive, CurrencyId = currency },
            TaxInclusiveAmount = new UblAmountType { Value = taxInclusive, CurrencyId = currency },
            PayableAmount = new UblAmountType { Value = taxInclusive, CurrencyId = currency },
        };

        return creditNote;
    }

    private static UblInvoiceLine MapLine(InvoiceLine line, string currency)
    {
        return new UblInvoiceLine
        {
            Id = line.LineNumber.ToString(CultureInfo.InvariantCulture),
            InvoicedQuantity = new UblMeasureType
            {
                Value = line.Quantity,
                UnitCode = line.Unit ?? "EA"
            },
            LineExtensionAmount = new UblAmountType
            {
                Value = line.LineTotal,
                CurrencyId = currency
            },
            Item = new UblItem
            {
                Description = line.Description,
                Name = line.Description,
                SellersItemIdentification = string.IsNullOrEmpty(line.ProductCode)
                    ? null
                    : new UblItemIdentification { Id = line.ProductCode },
                ClassifiedTaxCategory = new UblTaxCategory
                {
                    Id = line.TaxCategory ?? "S",
                    Percent = line.TaxRate,
                    TaxScheme = new UblTaxScheme { Id = "VAT" }
                },
            },
            Price = new UblPrice
            {
                PriceAmount = new UblAmountType
                {
                    Value = line.UnitPrice,
                    CurrencyId = currency
                }
            },
        };
    }

    private static void SerializeToStream<T>(Stream stream, T document, string targetNamespace) where T : class
    {
        var ns = new XmlSerializerNamespaces();
        ns.Add("cbc", UblNamespaces.Cbc);
        ns.Add("cac", UblNamespaces.Cac);

        var serializer = new XmlSerializer(typeof(T));

        var settings = new XmlWriterSettings
        {
            Indent = true,
            IndentChars = "  ",
            OmitXmlDeclaration = false,
            Encoding = System.Text.Encoding.UTF8,
        };

        using var streamWriter = new StreamWriter(stream, System.Text.Encoding.UTF8, bufferSize: 1024, leaveOpen: true);
        using var xmlWriter = XmlWriter.Create(streamWriter, settings);
        xmlWriter.WriteStartDocument();
        serializer.Serialize(xmlWriter, document, ns);
        xmlWriter.Flush();
    }
}
