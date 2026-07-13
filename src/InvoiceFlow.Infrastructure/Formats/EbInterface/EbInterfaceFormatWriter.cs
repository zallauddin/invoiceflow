using System.Globalization;
using System.Xml;
using System.Xml.Serialization;
using InvoiceFlow.Core.Entities;
using InvoiceFlow.Core.Enums;
using InvoiceFlow.Formats.Abstractions;
using InvoiceFlow.Infrastructure.Formats.EbInterface.Models;

namespace InvoiceFlow.Infrastructure.Formats.EbInterface;

/// <summary>
/// Writes core Invoice + InvoiceLine entities into an ebInterface XML stream.
/// Generates ebInterface 6.0 compliant XML with Austrian e-invoicing structure.
/// </summary>
public sealed class EbInterfaceFormatWriter : IFormatWriter
{
    /// <inheritdoc />
    public InvoiceFormat SupportedFormat => InvoiceFormat.EbInterface;

    /// <summary>Write invoice data to an ebInterface 6.0 XML stream.</summary>
    public Task<FormatWriteResult> WriteAsync(Invoice invoice, List<InvoiceLine> lines, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        ArgumentNullException.ThrowIfNull(lines);

        var stream = new MemoryStream();
        var validationResults = new List<ValidationResult>();

        var ebInvoice = MapToEbInvoice(invoice, lines);
        SerializeToStream(stream, ebInvoice);

        stream.Position = 0;

        var isCreditNote = invoice.DocumentType == DocumentType.CreditNote;
        var fileName = isCreditNote
            ? $"credit-note-{invoice.InvoiceNumber}.xml"
            : $"invoice-{invoice.InvoiceNumber}.xml";

        return Task.FromResult(new FormatWriteResult(
            stream,
            "application/xml",
            fileName,
            validationResults));
    }

    /// <summary>Map core Invoice + InvoiceLine entities to an ebInterface EbInvoice model.</summary>
    private static EbInvoice MapToEbInvoice(Invoice invoice, List<InvoiceLine> lines)
    {
        var currency = invoice.Currency ?? "EUR";

        var ebInvoice = new EbInvoice
        {
            InvoiceNumber = invoice.InvoiceNumber,
            InvoiceDate = invoice.InvoiceDate,
            DocumentType = MapDocumentType(invoice.DocumentType),
            Comment = invoice.Notes
        };

        // Biller (supplier / vendor)
        ebInvoice.Biller = new EbParty
        {
            VATIdentificationNumber = invoice.VendorTaxId,
            Name = invoice.VendorName,
            Address = new EbAddress()
        };

        // InvoiceRecipient (buyer / customer)
        ebInvoice.InvoiceRecipient = new EbParty
        {
            VATIdentificationNumber = invoice.BuyerTaxId,
            Name = invoice.BuyerName,
            Address = new EbAddress()
        };

        // Map lines
        ebInvoice.ListLineItem = new List<EbLineItem>(lines.Count);
        foreach (var line in lines)
        {
            ebInvoice.ListLineItem.Add(MapLineItem(line, currency));
        }

        // Compute totals
        var lineExtension = lines.Sum(l => l.LineTotal);
        var netAmount = invoice.Subtotal > 0 ? invoice.Subtotal : lineExtension;
        var taxAmount = invoice.TaxAmount > 0
            ? invoice.TaxAmount
            : Math.Max(0, invoice.TotalAmount - netAmount);
        var grossAmount = invoice.TotalAmount > 0
            ? invoice.TotalAmount
            : netAmount + taxAmount;

        ebInvoice.TotalNetAmount = netAmount;
        ebInvoice.TotalGrossAmount = grossAmount;

        // Document-level tax summary — aggregate by highest tax rate or use invoice-level
        if (taxAmount > 0)
        {
            ebInvoice.Tax = new EbTaxItem
            {
                Amount = taxAmount,
                TypeCode = "VAT"
            };
        }

        return ebInvoice;
    }

    /// <summary>Map a core InvoiceLine to an ebInterface EbLineItem.</summary>
    private static EbLineItem MapLineItem(InvoiceLine line, string currency)
    {
        var lineItem = new EbLineItem
        {
            LineNumber = line.LineNumber,
            Description = line.Description,
            UnitPrice = line.UnitPrice,
            LineTotalAmount = line.LineTotal,
            Quantity = new EbQuantity
            {
                Value = line.Quantity,
                Unit = line.Unit ?? "EA"
            }
        };

        // Line-level tax
        if (line.TaxRate > 0 || line.TaxAmount > 0)
        {
            lineItem.Tax = new EbLineTax
            {
                TaxRate = line.TaxRate,
                Taxes =
                [
                    new EbTaxItem
                    {
                        Amount = line.TaxAmount,
                        TypeCode = "VAT"
                    }
                ]
            };
        }

        return lineItem;
    }

    /// <summary>Map core DocumentType to ebInterface EbDocumentType.</summary>
    private static EbDocumentType MapDocumentType(DocumentType documentType)
    {
        return documentType switch
        {
            DocumentType.CreditNote => EbDocumentType.CreditNote,
            _ => EbDocumentType.Invoice
        };
    }

    /// <summary>Serialize an ebInterface model to an XML stream with proper namespace declarations.</summary>
    private static void SerializeToStream<T>(Stream stream, T document) where T : class
    {
        var ns = new XmlSerializerNamespaces();
        ns.Add(string.Empty, EbInterfaceNamespaces.V6);

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
