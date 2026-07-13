using System.Globalization;
using System.Xml;
using System.Xml.Serialization;
using InvoiceFlow.Core.Entities;
using InvoiceFlow.Core.Enums;
using InvoiceFlow.Formats.Abstractions;
using InvoiceFlow.Infrastructure.Formats.Zugferd.Models;

namespace InvoiceFlow.Infrastructure.Formats.Zugferd;

/// <summary>
/// Writes core Invoice + InvoiceLine entities into a ZUGFeRD/Factur-X CII XML stream.
/// Generates EN 16931-compliant CII XML (suitable for embedding in PDF/A-3).
/// Actual PDF/A-3 embedding would require a PDF library (e.g., iText7) and is a future enhancement.
/// </summary>
public sealed class ZugferdFormatWriter : IFormatWriter
{
    /// <summary>The format this writer supports.</summary>
    public InvoiceFormat SupportedFormat => InvoiceFormat.Zugferd;

    /// <summary>Write invoice data to a ZUGFeRD/Factur-X CII XML stream.</summary>
    public Task<FormatWriteResult> WriteAsync(Invoice invoice, List<InvoiceLine> lines, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        ArgumentNullException.ThrowIfNull(lines);

        var stream = new MemoryStream();
        var validationResults = new List<ValidationResult>();

        // Map core entities to CII
        var cii = MapToCii(invoice, lines);

        // Serialize to XML
        SerializeToStream(stream, cii);

        stream.Position = 0;

        var fileName = $"invoice-{invoice.InvoiceNumber}-zugferd.xml";

        return Task.FromResult(new FormatWriteResult(
            stream,
            "application/xml",
            fileName,
            validationResults));
    }

    /// <summary>Map core Invoice + InvoiceLine entities to a CII CrossIndustryInvoice.</summary>
    private static CiiCrossIndustryInvoice MapToCii(Invoice invoice, List<InvoiceLine> lines)
    {
        var currency = invoice.Currency ?? "EUR";
        var isCreditNote = invoice.DocumentType == DocumentType.CreditNote;

        var cii = new CiiCrossIndustryInvoice
        {
            // Document context — EN 16931 profile
            ExchangedDocumentContext = new CiiExchangedDocumentContext
            {
                GuidelineSpecifiedDocumentContextParameter = new CiiDocumentContextParameter
                {
                    Id = CiiNamespaces.En16931ProfileId
                }
            },

            // Exchanged document header
            ExchangedDocument = new CiiExchangedDocument
            {
                Id = invoice.InvoiceNumber,
                TypeCode = isCreditNote ? "381" : "380",
                IssueDateTime = new CiiDateTimeType
                {
                    DateTimeString = new CiiDateTimeString
                    {
                        Format = "102",
                        Value = invoice.InvoiceDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture)
                    }
                },
            },
        };

        // Add notes if present
        if (!string.IsNullOrWhiteSpace(invoice.Notes))
        {
            cii.ExchangedDocument.IncludedNotes.Add(new CiiNote
            {
                ContentLineCode = "916",
                Content = invoice.Notes
            });
        }

        // Supply chain trade transaction
        var transaction = new CiiSupplyChainTradeTransaction
        {
            // Header trade agreement
            ApplicableHeaderTradeAgreement = new CiiHeaderTradeAgreement
            {
                BuyerReference = invoice.ReferenceNumber,
                SellerTradeParty = MapSellerParty(invoice),
                BuyerTradeParty = MapBuyerParty(invoice),
                ApplicablePaymentTerms = invoice.DueDate.HasValue
                    ? new CiiPaymentTerms
                    {
                        DueDateDateTime = new CiiDateTimeType
                        {
                            DateTimeString = new CiiDateTimeString
                            {
                                Format = "102",
                                Value = invoice.DueDate.Value.ToString("yyyyMMdd", CultureInfo.InvariantCulture)
                            }
                        }
                    }
                    : null,
            },

            // Header trade delivery (minimal)
            ApplicableHeaderTradeDelivery = new CiiHeaderTradeDelivery(),

            // Header trade settlement
            ApplicableHeaderTradeSettlement = new CiiHeaderTradeSettlement
            {
                InvoiceCurrencyCode = currency,
            },
        };

        // Build tax summary from lines
        var taxGroups = lines
            .Where(l => l.TaxRate > 0)
            .GroupBy(l => l.TaxCategory ?? "S")
            .ToList();

        foreach (var group in taxGroups)
        {
            var taxableAmount = group.Sum(l => l.LineTotal);
            var taxAmount = Math.Round(taxableAmount * group.First().TaxRate / 100m, 2, MidpointRounding.AwayFromZero);

            transaction.ApplicableHeaderTradeSettlement.ApplicableTradeTaxes.Add(new CiiApplicableTradeTax
            {
                CalculatedAmount = new CiiAmountType { Value = taxAmount, CurrencyId = currency },
                BasisAmount = new CiiAmountType { Value = taxableAmount, CurrencyId = currency },
                ApplicablePercent = new CiiPercentType { Value = group.First().TaxRate },
                CategoryCode = group.Key,
                TypeCode = "VAT",
            });
        }

        // Monetary summation
        var lineExtension = lines.Sum(l => l.LineTotal);
        var taxBasis = invoice.Subtotal > 0 ? invoice.Subtotal : lineExtension;
        var taxTotal = invoice.TaxAmount > 0 ? invoice.TaxAmount : taxGroups.Sum(g =>
            Math.Round(g.Sum(l => l.LineTotal) * g.First().TaxRate / 100m, 2, MidpointRounding.AwayFromZero));
        var grandTotal = invoice.TotalAmount > 0 ? invoice.TotalAmount : taxBasis + taxTotal;

        transaction.ApplicableHeaderTradeSettlement.SpecifiedTradeSettlementHeaderMonetarySummation =
            new CiiMonetarySummation
            {
                TaxBasisTotalAmount = new CiiAmountType { Value = taxBasis, CurrencyId = currency },
                TaxTotalAmount = new CiiAmountType { Value = taxTotal, CurrencyId = currency },
                GrandTotalAmount = new CiiAmountType { Value = grandTotal, CurrencyId = currency },
                DuePayableAmount = new CiiAmountType { Value = grandTotal, CurrencyId = currency },
            };

        // Map line items
        foreach (var line in lines)
        {
            transaction.IncludedSupplyChainTradeLineItems.Add(MapLineItem(line, currency));
        }

        cii.SupplyChainTradeTransaction = transaction;
        return cii;
    }

    /// <summary>Map the seller (vendor) to a CII TradeParty.</summary>
    private static CiiTradeParty MapSellerParty(Invoice invoice)
    {
        var party = new CiiTradeParty
        {
            Name = invoice.VendorName,
        };

        if (!string.IsNullOrWhiteSpace(invoice.VendorEmail))
        {
            party.DefinedTradeContact = new CiiTradeContact
            {
                URIUniversalCommunication = new CiiUriUniversalCommunication
                {
                    UriId = $"mailto:{invoice.VendorEmail}"
                }
            };
        }

        if (!string.IsNullOrWhiteSpace(invoice.VendorTaxId))
        {
            party.SpecifiedTaxRegistrations.Add(new CiiTaxRegistration { Id = invoice.VendorTaxId });
        }

        return party;
    }

    /// <summary>Map the buyer to a CII TradeParty.</summary>
    private static CiiTradeParty MapBuyerParty(Invoice invoice)
    {
        var party = new CiiTradeParty
        {
            Name = invoice.BuyerName,
        };

        if (!string.IsNullOrWhiteSpace(invoice.BuyerTaxId))
        {
            party.SpecifiedTaxRegistrations.Add(new CiiTaxRegistration { Id = invoice.BuyerTaxId });
        }

        return party;
    }

    /// <summary>Map a core InvoiceLine to a CII TradeLineItem.</summary>
    private static CiiTradeLineItem MapLineItem(InvoiceLine line, string currency)
    {
        return new CiiTradeLineItem
        {
            AssociatedDocumentLineDocument = new CiiDocumentLineDocument
            {
                LineId = line.LineNumber.ToString(CultureInfo.InvariantCulture)
            },

            SpecifiedTradeProduct = new CiiTradeProduct
            {
                Name = line.Description,
                Description = line.Description,
                SellerAssignedId = line.ProductCode,
            },

            SpecifiedLineTradeAgreement = new CiiLineTradeAgreement
            {
                NetPriceProductTradePrice = new CiiProductTradePrice
                {
                    ChargeAmount = new CiiAmountType
                    {
                        Value = line.UnitPrice,
                        CurrencyId = currency
                    }
                }
            },

            SpecifiedLineTradeDelivery = new CiiLineTradeDelivery
            {
                BilledQuantity = new CiiQuantityType
                {
                    Value = line.Quantity,
                    UnitCode = line.Unit ?? "EA"
                }
            },

            SpecifiedLineTradeSettlement = new CiiLineTradeSettlement
            {
                ApplicableTradeTax = new CiiLineTradeTax
                {
                    TypeCode = "VAT",
                    CategoryCode = line.TaxCategory ?? "S",
                    ApplicablePercent = new CiiPercentType { Value = line.TaxRate }
                },
                SpecifiedTradeSettlementLineMonetarySummation = new CiiLineMonetarySummation
                {
                    LineTotalAmount = new CiiAmountType
                    {
                        Value = line.LineTotal,
                        CurrencyId = currency
                    }
                }
            }
        };
    }

    /// <summary>Serialize a CII CrossIndustryInvoice to the output stream.</summary>
    private static void SerializeToStream(Stream stream, CiiCrossIndustryInvoice document)
    {
        var ns = new XmlSerializerNamespaces();
        ns.Add("rsm", CiiNamespaces.Rsm);
        ns.Add("ram", CiiNamespaces.Ram);
        ns.Add("udt", CiiNamespaces.Udt);
        ns.Add("qdt", CiiNamespaces.Qdt);

        var serializer = new XmlSerializer(typeof(CiiCrossIndustryInvoice));

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
