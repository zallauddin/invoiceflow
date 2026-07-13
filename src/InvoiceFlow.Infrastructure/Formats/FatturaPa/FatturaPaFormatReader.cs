using System.Globalization;
using System.Xml.Serialization;
using InvoiceFlow.Core.Entities;
using InvoiceFlow.Core.Enums;
using InvoiceFlow.Formats.Abstractions;
using InvoiceFlow.Infrastructure.Formats.FatturaPa.Models;

namespace InvoiceFlow.Infrastructure.Formats.FatturaPa;

/// <summary>
/// Reads a FatturaPA v1.2 XML stream and maps it to core entity types.
/// Handles the Italian mandatory e-invoicing format with p:FatturaElettronica root element.
/// </summary>
public sealed class FatturaPaFormatReader : IFormatReader
{
    /// <inheritdoc />
    public InvoiceFormat SupportedFormat => InvoiceFormat.FatturaPA;

    /// <summary>Read and parse a FatturaPA XML stream into Invoice + InvoiceLine entities.</summary>
    public async Task<FormatReadResult> ReadAsync(Stream content, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        // Read stream into memory so we can inspect root element and deserialize
        await using var ms = new MemoryStream();
        await content.CopyToAsync(ms, ct);
        ms.Position = 0;

        // Read raw XML string for rawXml output and format detection
        using var reader = new StreamReader(ms, leaveOpen: true);
        var rawXml = await reader.ReadToEndAsync(ct);
        ms.Position = 0;

        // Validate this is FatturaPA format
        if (!rawXml.Contains("FatturaElettronica", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The XML document does not appear to be a FatturaPA document (root element 'FatturaElettronica' not found).");
        }

        // Deserialize the root document
        var fattura = DeserializeFromStream<FatturaElettronica>(ms);

        var validationResults = new List<ValidationResult>();
        ValidateFatturaPaVersion(fattura.Versione, validationResults);

        // Process the first body (FatturaPA allows multiple bodies, but we map the first)
        if (fattura.Bodies.Count == 0)
        {
            throw new InvalidOperationException("FatturaPA document contains no invoice bodies (FatturaElettronicaBody).");
        }

        var body = fattura.Bodies[0];
        var coreInvoice = MapToInvoice(fattura.Header, body);
        var lines = MapToInvoiceLines(body, coreInvoice.Id);

        // Collect Italian-specific metadata
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        CollectMetadata(fattura.Header, body, metadata);

        return new FormatReadResult(coreInvoice, lines, rawXml, metadata, validationResults);
    }

    /// <summary>Map FatturaPA header + body to a core Invoice entity.</summary>
    private static Invoice MapToInvoice(FatturaElettronicaHeaderType header, FatturaElettronicaBodyType body)
    {
        var datiGenerali = body.DatiGenerali.DatiGeneraliDocumento;

        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            InvoiceNumber = datiGenerali.Numero,
            DocumentType = MapDocumentType(datiGenerali.TipoDocumento),
            InvoiceDate = datiGenerali.Data,
            Currency = datiGenerali.Divisa ?? "EUR",
        };

        // Map vendor (CedentePrestatore)
        var cedente = header.CedentePrestatore;
        invoice.VendorName = cedente.DatiAnagrafici.Anagrafica.Denominazione
            ?? CombineName(cedente.DatiAnagrafici.Anagrafica.Nome, cedente.DatiAnagrafici.Anagrafica.Cognome)
            ?? string.Empty;
        invoice.VendorTaxId = cedente.DatiAnagrafici.IdFiscaleIVA?.IdCodice
            ?? cedente.DatiAnagrafici.CodiceFiscale;

        // Map buyer (CessionarioCommittente)
        var cessionario = header.CessionarioCommittente;
        invoice.BuyerName = cessionario.DatiAnagrafici.Anagrafica.Denominazione
            ?? CombineName(cessionario.DatiAnagrafici.Anagrafica.Nome, cessionario.DatiAnagrafici.Anagrafica.Cognome)
            ?? string.Empty;
        invoice.BuyerTaxId = cessionario.DatiAnagrafici.IdFiscaleIVA?.IdCodice
            ?? cessionario.DatiAnagrafici.CodiceFiscale;

        // Map financial totals from DatiRiepilogo
        var riepilogo = body.DatiBeniServizi.DatiRiepilogo;
        invoice.Subtotal = riepilogo.Sum(r => r.ImponibileImporto);
        invoice.TaxAmount = riepilogo.Sum(r => r.Imposta);
        invoice.TotalAmount = datiGenerali.ImportoTotaleDocumento
            ?? (invoice.Subtotal + invoice.TaxAmount);

        // Set country code from seller's address
        invoice.CountryCode = cedente.Sede.Nazione ?? "IT";

        // Map reference from related documents
        invoice.ReferenceNumber = body.DatiGenerali.DatiOrdineAcquisto?.RiferimentoNumero
            ?? body.DatiGenerali.DatiContratto?.RiferimentoNumero;

        // Notes from Causale
        invoice.Notes = datiGenerali.Causale;

        return invoice;
    }

    /// <summary>Map FatturaPA line items to core InvoiceLine entities.</summary>
    private static List<InvoiceLine> MapToInvoiceLines(FatturaElettronicaBodyType body, Guid invoiceId)
    {
        var dettaglioLinee = body.DatiBeniServizi.DettaglioLinee;
        var lines = new List<InvoiceLine>(dettaglioLinee.Count);

        foreach (var linea in dettaglioLinee)
        {
            var line = new InvoiceLine
            {
                Id = Guid.NewGuid(),
                InvoiceId = invoiceId,
                LineNumber = linea.NumeroLinea,
                Description = linea.Descrizione,
                ProductCode = linea.CodiceArticolo?.FirstOrDefault()?.CodiceValore,
                Quantity = linea.Quantita ?? 1m,
                Unit = linea.UnitaMisura,
                UnitPrice = linea.PrezzoUnitario,
                LineTotal = linea.PrezzoTotale,
                TaxRate = linea.AliquotaIVA,
                TaxCategory = linea.Natura,
            };
            lines.Add(line);
        }

        return lines;
    }

    /// <summary>Map FatturaPA TipoDocumento to core DocumentType.</summary>
    private static DocumentType MapDocumentType(string tipoDocumento)
    {
        return tipoDocumento?.ToUpperInvariant() switch
        {
            "TD01" => DocumentType.Invoice,
            "TD02" => DocumentType.Invoice,
            "TD03" => DocumentType.Invoice,
            "TD04" => DocumentType.CreditNote,
            "TD05" => DocumentType.DebitNote,
            "TD06" => DocumentType.Invoice,
            _ => DocumentType.Invoice,
        };
    }

    /// <summary>Combine first name and surname into a full name string.</summary>
    private static string? CombineName(string? nome, string? cognome)
    {
        if (string.IsNullOrWhiteSpace(nome) && string.IsNullOrWhiteSpace(cognome))
            return null;

        return $"{nome} {cognome}".Trim();
    }

    /// <summary>Collect Italian-specific metadata from the FatturaPA document.</summary>
    private static void CollectMetadata(
        FatturaElettronicaHeaderType header,
        FatturaElettronicaBodyType body,
        Dictionary<string, string> metadata)
    {
        // Transmission data
        metadata["FormatoTrasmissione"] = header.DatiTrasmissione.FormatoTrasmissione;
        metadata["CodiceDestinatario"] = header.DatiTrasmissione.CodiceDestinatario;
        metadata["ProgressivoInvio"] = header.DatiTrasmissione.ProgressivoInvio;
        metadata["IdTrasmittentePaese"] = header.DatiTrasmissione.IdTrasmittente.IdPaese;
        metadata["IdTrasmittenteCodice"] = header.DatiTrasmissione.IdTrasmittente.IdCodice;

        if (!string.IsNullOrEmpty(header.DatiTrasmissione.PECDestinatario))
            metadata["PECDestinatario"] = header.DatiTrasmissione.PECDestinatario;

        // Seller fiscal data
        var cedenteAnagrafici = header.CedentePrestatore.DatiAnagrafici;
        if (!string.IsNullOrEmpty(cedenteAnagrafici.IdFiscaleIVA?.IdPaese))
            metadata["CedenteIdPaese"] = cedenteAnagrafici.IdFiscaleIVA.IdPaese;
        if (!string.IsNullOrEmpty(cedenteAnagrafici.IdFiscaleIVA?.IdCodice))
            metadata["CedenteIdCodice"] = cedenteAnagrafici.IdFiscaleIVA.IdCodice;
        if (!string.IsNullOrEmpty(cedenteAnagrafici.CodiceFiscale))
            metadata["CedenteCodiceFiscale"] = cedenteAnagrafici.CodiceFiscale;
        metadata["RegimeFiscale"] = cedenteAnagrafici.RegimeFiscale;

        // Buyer fiscal data
        var cessionarioAnagrafici = header.CessionarioCommittente.DatiAnagrafici;
        if (!string.IsNullOrEmpty(cessionarioAnagrafici.IdFiscaleIVA?.IdPaese))
            metadata["CessionarioIdPaese"] = cessionarioAnagrafici.IdFiscaleIVA.IdPaese;
        if (!string.IsNullOrEmpty(cessionarioAnagrafici.IdFiscaleIVA?.IdCodice))
            metadata["CessionarioIdCodice"] = cessionarioAnagrafici.IdFiscaleIVA.IdCodice;
        if (!string.IsNullOrEmpty(cessionarioAnagrafici.CodiceFiscale))
            metadata["CessionarioCodiceFiscale"] = cessionarioAnagrafici.CodiceFiscale;

        // Document type
        metadata["TipoDocumento"] = body.DatiGenerali.DatiGeneraliDocumento.TipoDocumento;
    }

    /// <summary>Validate the FatturaPA version string.</summary>
    private static void ValidateFatturaPaVersion(string? versione, List<ValidationResult> results)
    {
        if (string.IsNullOrWhiteSpace(versione))
        {
            results.Add(new ValidationResult(
                "FP-0", "FatturaPA version attribute is missing.",
                ValidationSeverity.Warning, "/*/@versione"));
        }
        else if (versione is not ("FPA12" or "FPR12"))
        {
            results.Add(new ValidationResult(
                "FP-0", $"Expected FPA12 or FPR12, got '{versione}'.",
                ValidationSeverity.Warning, "/*/@versione", versione));
        }
    }

    private static T DeserializeFromStream<T>(Stream stream) where T : class
    {
        var serializer = new XmlSerializer(typeof(T));
        using var reader = new StreamReader(stream, leaveOpen: true);
        return (T)serializer.Deserialize(reader)!;
    }
}
