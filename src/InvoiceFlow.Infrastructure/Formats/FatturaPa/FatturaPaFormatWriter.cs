using System.Globalization;
using System.Xml;
using System.Xml.Serialization;
using InvoiceFlow.Core.Entities;
using InvoiceFlow.Core.Enums;
using InvoiceFlow.Formats.Abstractions;
using InvoiceFlow.Infrastructure.Formats.FatturaPa.Models;

namespace InvoiceFlow.Infrastructure.Formats.FatturaPa;

/// <summary>
/// Writes core Invoice + InvoiceLine entities into a FatturaPA v1.2 XML stream.
/// Generates Agenzia delle Entrate-compliant p:FatturaElettronica XML.
/// </summary>
public sealed class FatturaPaFormatWriter : IFormatWriter
{
    /// <inheritdoc />
    public InvoiceFormat SupportedFormat => InvoiceFormat.FatturaPA;

    /// <summary>Standard domestic transmission format code.</summary>
    private const string FormatoTrasmissioneDomestico = "FPA12";

    /// <summary>Cross-border / PA transmission format code.</summary>
    private const string FormatoTrasmissioneEstero = "FPR12";

    /// <summary>Default TipoDocumento for invoices.</summary>
    private const string TipoDocumentoFattura = "TD01";

    /// <summary>TipoDocumento for credit notes.</summary>
    private const string TipoDocumentoNotaCredito = "TD04";

    /// <summary>TipoDocumento for debit notes.</summary>
    private const string TipoDocumentoNotaDebito = "TD05";

    /// <summary>Write invoice data to a FatturaPA v1.2 XML stream.</summary>
    public Task<FormatWriteResult> WriteAsync(Invoice invoice, List<InvoiceLine> lines, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        ArgumentNullException.ThrowIfNull(lines);

        var stream = new MemoryStream();
        var validationResults = new List<ValidationResult>();

        var fattura = MapToFatturaElettronica(invoice, lines);

        SerializeToStream(stream, fattura);

        stream.Position = 0;

        var isCreditNote = invoice.DocumentType == DocumentType.CreditNote;
        var fileName = isCreditNote
            ? $"notacredita-{invoice.InvoiceNumber}.xml"
            : $"fattura-{invoice.InvoiceNumber}.xml";

        return Task.FromResult(new FormatWriteResult(
            stream,
            "application/xml",
            fileName,
            validationResults));
    }

    /// <summary>Map core Invoice + InvoiceLine entities to a FatturaElettronica root document.</summary>
    private static FatturaElettronica MapToFatturaElettronica(Invoice invoice, List<InvoiceLine> lines)
    {
        var fattura = new FatturaElettronica
        {
            Versione = FormatoTrasmissioneDomestico,
            Header = MapHeader(invoice),
            Bodies = [MapBody(invoice, lines)],
        };

        return fattura;
    }

    /// <summary>Map invoice vendor/buyer to FatturaElettronicaHeader.</summary>
    private static FatturaElettronicaHeaderType MapHeader(Invoice invoice)
    {
        return new FatturaElettronicaHeaderType
        {
            DatiTrasmissione = new DatiTrasmissioneType
            {
                IdTrasmittente = new IdTrasmittenteType
                {
                    IdPaese = "IT",
                    IdCodice = invoice.VendorTaxId ?? "00000000000",
                },
                ProgressivoInvio = "00001",
                FormatoTrasmissione = FormatoTrasmissioneDomestico,
                CodiceDestinatario = "0000000",
            },
            CedentePrestatore = MapCedentePrestatore(invoice),
            CessionarioCommittente = MapCessionarioCommittente(invoice),
        };
    }

    /// <summary>Map vendor to CedentePrestatore (seller/supplier).</summary>
    private static CedentePrestatoreType MapCedentePrestatore(Invoice invoice)
    {
        var cedente = new CedentePrestatoreType
        {
            DatiAnagrafici = new DatiAnagraficiCedenteType
            {
                IdFiscaleIVA = new IdFiscaleIVAType
                {
                    IdPaese = "IT",
                    IdCodice = invoice.VendorTaxId ?? "00000000000",
                },
                Anagrafica = new AnagraficaType
                {
                    Denominazione = invoice.VendorName,
                },
                RegimeFiscale = "RF01",
            },
            Sede = new SedeType
            {
                Indirizzo = "Via non specificata",
                CAP = "00100",
                Comune = "Roma",
                Nazione = invoice.CountryCode ?? "IT",
            },
        };

        return cedente;
    }

    /// <summary>Map buyer to CessionarioCommittente (buyer/customer).</summary>
    private static CessionarioCommittenteType MapCessionarioCommittente(Invoice invoice)
    {
        var cessionario = new CessionarioCommittenteType
        {
            DatiAnagrafici = new DatiAnagraficiCessionarioType
            {
                Anagrafica = new AnagraficaType
                {
                    Denominazione = invoice.BuyerName,
                },
            },
            Sede = new SedeType
            {
                Indirizzo = "Via non specificata",
                CAP = "00100",
                Comune = "Roma",
                Nazione = "IT",
            },
        };

        if (!string.IsNullOrEmpty(invoice.BuyerTaxId))
        {
            cessionario.DatiAnagrafici.IdFiscaleIVA = new IdFiscaleIVAType
            {
                IdPaese = "IT",
                IdCodice = invoice.BuyerTaxId,
            };
        }

        return cessionario;
    }

    /// <summary>Map invoice + lines to FatturaElettronicaBody.</summary>
    private static FatturaElettronicaBodyType MapBody(Invoice invoice, List<InvoiceLine> lines)
    {
        return new FatturaElettronicaBodyType
        {
            DatiGenerali = MapDatiGenerali(invoice),
            DatiBeniServizi = MapDatiBeniServizi(lines),
        };
    }

    /// <summary>Map invoice to DatiGenerali (general document data).</summary>
    private static DatiGeneraliType MapDatiGenerali(Invoice invoice)
    {
        return new DatiGeneraliType
        {
            DatiGeneraliDocumento = new DatiGeneraliDocumentoType
            {
                TipoDocumento = MapDocumentType(invoice.DocumentType),
                Divisa = invoice.Currency ?? "EUR",
                Data = invoice.InvoiceDate,
                Numero = invoice.InvoiceNumber,
                ImportoTotaleDocumento = invoice.TotalAmount > 0 ? invoice.TotalAmount : null,
                Causale = invoice.Notes,
            },
        };
    }

    /// <summary>Map lines to DatiBeniServizi (goods/services data).</summary>
    private static DatiBeniServiziType MapDatiBeniServizi(List<InvoiceLine> lines)
    {
        var dettaglioLinee = new List<DettaglioLineaType>(lines.Count);
        var riepilogoMap = new Dictionary<string, DatiRiepilogoType>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in lines)
        {
            dettaglioLinee.Add(MapDettaglioLinea(line));

            // Aggregate tax summary by AliquotaIVA + Natura
            var riepilogoKey = $"{line.TaxRate}_{line.TaxCategory ?? "S"}";
            if (!riepilogoMap.TryGetValue(riepilogoKey, out var riepilogo))
            {
                riepilogo = new DatiRiepilogoType
                {
                    AliquotaIVA = line.TaxRate,
                    Natura = line.TaxRate == 0m ? (line.TaxCategory ?? "N1") : null,
                    ImponibileImporto = 0m,
                    Imposta = 0m,
                };
                riepilogoMap[riepilogoKey] = riepilogo;
            }

            riepilogo.ImponibileImporto += line.LineTotal;
            riepilogo.Imposta += line.TaxAmount;
        }

        return new DatiBeniServiziType
        {
            DettaglioLinee = dettaglioLinee,
            DatiRiepilogo = riepilogoMap.Values.ToList(),
        };
    }

    /// <summary>Map a core InvoiceLine to a FatturaPA DettaglioLinea.</summary>
    private static DettaglioLineaType MapDettaglioLinea(InvoiceLine line)
    {
        var linea = new DettaglioLineaType
        {
            NumeroLinea = line.LineNumber,
            Descrizione = line.Description,
            PrezzoUnitario = line.UnitPrice,
            PrezzoTotale = line.LineTotal,
            AliquotaIVA = line.TaxRate,
        };

        if (line.Quantity > 0)
        {
            linea.Quantita = line.Quantity;
        }

        if (!string.IsNullOrEmpty(line.Unit))
        {
            linea.UnitaMisura = line.Unit;
        }

        if (line.TaxRate == 0m)
        {
            linea.Natura = line.TaxCategory ?? "N1";
        }

        if (!string.IsNullOrEmpty(line.ProductCode))
        {
            linea.CodiceArticolo =
            [
                new CodiceArticoloType
                {
                    CodiceValore = line.ProductCode,
                    CodiceTipo = "SKU",
                },
            ];
        }

        return linea;
    }

    /// <summary>Map core DocumentType to FatturaPA TipoDocumento code.</summary>
    private static string MapDocumentType(DocumentType documentType)
    {
        return documentType switch
        {
            DocumentType.Invoice => TipoDocumentoFattura,
            DocumentType.CreditNote => TipoDocumentoNotaCredito,
            DocumentType.DebitNote => TipoDocumentoNotaDebito,
            _ => TipoDocumentoFattura,
        };
    }

    /// <summary>Serialize FatturaElettronica to XML stream with p: namespace prefix.</summary>
    private static void SerializeToStream(Stream stream, FatturaElettronica fattura)
    {
        var ns = new XmlSerializerNamespaces();
        ns.Add("p", FatturaPaNamespaces.Root);
        ns.Add("ds", FatturaPaNamespaces.Dsig);

        var serializer = new XmlSerializer(typeof(FatturaElettronica));

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
        serializer.Serialize(xmlWriter, fattura, ns);
        xmlWriter.Flush();
    }
}
