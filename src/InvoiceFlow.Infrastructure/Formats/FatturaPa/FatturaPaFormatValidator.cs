using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using InvoiceFlow.Formats.Abstractions;
using InvoiceFlow.Infrastructure.Formats.FatturaPa.Models;

namespace InvoiceFlow.Infrastructure.Formats.FatturaPa;

/// <summary>
/// Validates a FatturaPA v1.2 XML stream against Agenzia delle Entrate structural and business rules.
/// Performs structural XML validation and Italian-specific rule checks.
/// </summary>
public sealed class FatturaPaFormatValidator : IFormatValidator
{
    /// <inheritdoc />
    public InvoiceFormat SupportedFormat => InvoiceFormat.FatturaPA;

    /// <summary>Validate FatturaPA XML content against Agenzia delle Entrate rules.</summary>
    public async Task<FormatValidationResult> ValidateAsync(Stream content, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        await using var ms = new MemoryStream();
        await content.CopyToAsync(ms, ct);
        ms.Position = 0;

        var results = new List<ValidationResult>();

        ValidateXmlStructure(ms, results);
        ms.Position = 0;

        var rootXml = await ReadRootXml(ms, ct);
        if (!rootXml.Contains("FatturaElettronica", StringComparison.OrdinalIgnoreCase))
        {
            results.Add(new ValidationResult("STRUCT-1", "Root element is not 'FatturaElettronica'.", ValidationSeverity.Error, "/*"));
            return BuildResult(results);
        }

        FatturaElettronica? fattura = null;
        try
        {
            ms.Position = 0;
            fattura = DeserializeFromStream<FatturaElettronica>(ms);
        }
        catch (InvalidOperationException ex)
        {
            results.Add(new ValidationResult("STRUCT-2", $"XML deserialization failed: {ex.Message}", ValidationSeverity.Error));
            return BuildResult(results);
        }

        ValidateBusinessRules(fattura, results);
        return BuildResult(results);
    }

    private static void ValidateXmlStructure(MemoryStream stream, List<ValidationResult> results)
    {
        try
        {
            var settings = new XmlReaderSettings { ValidationType = ValidationType.None, DtdProcessing = DtdProcessing.Prohibit };
            using var reader = XmlReader.Create(stream, settings);
            while (reader.Read()) { }
        }
        catch (XmlException ex)
        {
            results.Add(new ValidationResult("STRUCT-0", $"XML is not well-formed: {ex.Message}", ValidationSeverity.Error, null, $"Line {ex.LineNumber}, Position {ex.LinePosition}"));
        }
    }

    private static async Task<string> ReadRootXml(MemoryStream stream, CancellationToken ct)
    {
        using var reader = new StreamReader(stream, leaveOpen: true);
        var buffer = new char[4096];
        var bytesRead = await reader.ReadAsync(buffer, ct);
        return new string(buffer, 0, bytesRead);
    }

    private static void ValidateBusinessRules(FatturaElettronica fattura, List<ValidationResult> results)
    {
        ValidateDatiTrasmissione(fattura.Header.DatiTrasmissione, results);
        ValidateCedentePrestatore(fattura.Header.CedentePrestatore, results);

        if (fattura.Bodies.Count > 0)
        {
            ValidateDatiGeneraliDocumento(fattura.Bodies[0].DatiGenerali.DatiGeneraliDocumento, results);
            ValidateDatiBeniServizi(fattura.Bodies[0].DatiBeniServizi, results);
        }
        else
        {
            results.Add(new ValidationResult("FP-BODY", "FatturaPA document contains no invoice bodies.", ValidationSeverity.Error));
        }
    }

    private static void ValidateDatiTrasmissione(DatiTrasmissioneType dt, List<ValidationResult> results)
    {
        // 4.1.1: IdTrasmittente
        if (string.IsNullOrWhiteSpace(dt.IdTrasmittente.IdPaese) || dt.IdTrasmittente.IdPaese.Length != 2)
            results.Add(new ValidationResult("FP-4.1.1a", $"IdTrasmittente.IdPaese must be 2 chars, got '{dt.IdTrasmittente.IdPaese}'.", ValidationSeverity.Error, "/DatiTrasmissione/IdTrasmittente/IdPaese"));

        if (string.IsNullOrWhiteSpace(dt.IdTrasmittente.IdCodice) || dt.IdTrasmittente.IdCodice.Length > 28)
            results.Add(new ValidationResult("FP-4.1.1b", $"IdTrasmittente.IdCodice is required (1-28 chars).", ValidationSeverity.Error, "/DatiTrasmissione/IdTrasmittente/IdCodice"));

        // 4.1.2: ProgressivoInvio
        if (string.IsNullOrWhiteSpace(dt.ProgressivoInvio))
            results.Add(new ValidationResult("FP-4.1.2", "ProgressivoInvio is required.", ValidationSeverity.Error, "/DatiTrasmissione/ProgressivoInvio"));

        // 4.1.3: FormatoTrasmissione
        if (string.IsNullOrWhiteSpace(dt.FormatoTrasmissione))
            results.Add(new ValidationResult("FP-4.1.3", "FormatoTrasmissione is required.", ValidationSeverity.Error, "/DatiTrasmissione/FormatoTrasmissione"));
        else if (dt.FormatoTrasmissione is not ("FPA12" or "FPR12"))
            results.Add(new ValidationResult("FP-4.1.3", $"FormatoTrasmissione must be 'FPA12' or 'FPR12', got '{dt.FormatoTrasmissione}'.", ValidationSeverity.Error, "/DatiTrasmissione/FormatoTrasmissione", dt.FormatoTrasmissione));

        // 4.1.4: CodiceDestinatario
        if (string.IsNullOrWhiteSpace(dt.CodiceDestinatario))
            results.Add(new ValidationResult("FP-4.1.4", "CodiceDestinatario is required.", ValidationSeverity.Error, "/DatiTrasmissione/CodiceDestinatario"));
        else if (dt.CodiceDestinatario.Length is < 6 or > 7)
            results.Add(new ValidationResult("FP-4.1.4", $"CodiceDestinatario must be 6-7 chars, got {dt.CodiceDestinatario.Length}.", ValidationSeverity.Warning, "/DatiTrasmissione/CodiceDestinatario", dt.CodiceDestinatario));
    }

    private static void ValidateCedentePrestatore(CedentePrestatoreType cedente, List<ValidationResult> results)
    {
        var anagrafici = cedente.DatiAnagrafici;
        var hasPartitaIva = !string.IsNullOrWhiteSpace(anagrafici.IdFiscaleIVA?.IdCodice);
        var hasCodiceFiscale = !string.IsNullOrWhiteSpace(anagrafici.CodiceFiscale);

        // 4.2.1: Must have either IdFiscaleIVA or CodiceFiscale
        if (!hasPartitaIva && !hasCodiceFiscale)
            results.Add(new ValidationResult("FP-4.2.1", "CedentePrestatore must have either IdFiscaleIVA or CodiceFiscale.", ValidationSeverity.Error, "/CedentePrestatore/DatiAnagrafici"));

        // 4.2.2: Italian PartitaIVA must be 11 digits
        if (hasPartitaIva && anagrafici.IdFiscaleIVA!.IdPaese == "IT")
        {
            if (anagrafici.IdFiscaleIVA.IdCodice.Length != 11 || !anagrafici.IdFiscaleIVA.IdCodice.All(char.IsDigit))
                results.Add(new ValidationResult("FP-4.2.2", $"Italian PartitaIVA must be 11 digits, got '{anagrafici.IdFiscaleIVA.IdCodice}'.", ValidationSeverity.Error, "/CedentePrestatore/DatiAnagrafici/IdFiscaleIVA/IdCodice", anagrafici.IdFiscaleIVA.IdCodice));
        }

        // 4.2.3: CodiceFiscale must be 11 or 16 chars
        if (hasCodiceFiscale)
        {
            var cf = anagrafici.CodiceFiscale!;
            if (cf.Length is not (11 or 16))
                results.Add(new ValidationResult("FP-4.2.3", $"CodiceFiscale must be 11 or 16 chars, got {cf.Length}.", ValidationSeverity.Error, "/CedentePrestatore/DatiAnagrafici/CodiceFiscale", cf));
        }

        // Sede validation
        if (string.IsNullOrWhiteSpace(cedente.Sede.Indirizzo))
            results.Add(new ValidationResult("FP-4.2.4", "CedentePrestatore.Sede.Indirizzo is required.", ValidationSeverity.Error, "/CedentePrestatore/Sede/Indirizzo"));

        if (string.IsNullOrWhiteSpace(cedente.Sede.CAP) || cedente.Sede.CAP.Length != 5)
            results.Add(new ValidationResult("FP-4.2.5", $"CedentePrestatore.Sede.CAP must be 5 digits, got '{cedente.Sede.CAP}'.", ValidationSeverity.Warning, "/CedentePrestatore/Sede/CAP"));
    }

    private static void ValidateDatiGeneraliDocumento(DatiGeneraliDocumentoType datiGen, List<ValidationResult> results)
    {
        // 4.3.1: Numero required
        if (string.IsNullOrWhiteSpace(datiGen.Numero))
            results.Add(new ValidationResult("FP-4.3.1", "DatiGeneraliDocumento.Numero is required.", ValidationSeverity.Error, "/DatiGenerali/DatiGeneraliDocumento/Numero"));

        // 4.3.2: Data required
        if (datiGen.Data == default)
            results.Add(new ValidationResult("FP-4.3.2", "DatiGeneraliDocumento.Data is required.", ValidationSeverity.Error, "/DatiGenerali/DatiGeneraliDocumento/Data"));

        // 4.3.3: TipoDocumento required
        if (string.IsNullOrWhiteSpace(datiGen.TipoDocumento))
            results.Add(new ValidationResult("FP-4.3.3", "DatiGeneraliDocumento.TipoDocumento is required.", ValidationSeverity.Error, "/DatiGenerali/DatiGeneraliDocumento/TipoDocumento"));
        else if (datiGen.TipoDocumento is not ("TD01" or "TD02" or "TD03" or "TD04" or "TD05" or "TD06" or "TD07" or "TD08" or "TD09" or "TD10" or "TD16" or "TD17" or "TD18" or "TD19" or "TD20" or "TD21" or "TD22" or "TD23" or "TD24" or "TD25" or "TD26" or "TD27" or "TD28"))
            results.Add(new ValidationResult("FP-4.3.3", $"TipoDocumento '{datiGen.TipoDocumento}' is not a valid FatturaPA document type.", ValidationSeverity.Warning, "/DatiGenerali/DatiGeneraliDocumento/TipoDocumento", datiGen.TipoDocumento));

        // 4.3.4: Divisa required
        if (string.IsNullOrWhiteSpace(datiGen.Divisa))
            results.Add(new ValidationResult("FP-4.3.4", "DatiGeneraliDocumento.Divisa is required.", ValidationSeverity.Error, "/DatiGenerali/DatiGeneraliDocumento/Divisa"));
    }

    private static void ValidateDatiBeniServizi(DatiBeniServiziType dbs, List<ValidationResult> results)
    {
        // 4.4.1: Each DettaglioLinea must have NumeroLinea
        for (var i = 0; i < dbs.DettaglioLinee.Count; i++)
        {
            var linea = dbs.DettaglioLinee[i];
            var path = $"/DatiBeniServizi/DettaglioLinee[{i + 1}]";

            if (linea.NumeroLinea <= 0)
                results.Add(new ValidationResult("FP-4.4.1", $"DettaglioLinea {i + 1}: NumeroLinea must be > 0.", ValidationSeverity.Error, $"{path}/NumeroLinea"));

            // 4.4.2: Each line must have AliquotaIVA or Natura
            if (linea.AliquotaIVA == 0m && string.IsNullOrWhiteSpace(linea.Natura))
                results.Add(new ValidationResult("FP-4.4.2", $"DettaglioLinea {i + 1}: when AliquotaIVA=0, Natura is required.", ValidationSeverity.Error, $"{path}/Natura"));

            if (string.IsNullOrWhiteSpace(linea.Descrizione))
                results.Add(new ValidationResult("FP-4.4.3", $"DettaglioLinea {i + 1}: Descrizione is required.", ValidationSeverity.Warning, $"{path}/Descrizione"));
        }

        // 4.4.4: At least one DatiRiepilogo
        if (dbs.DatiRiepilogo.Count == 0)
        {
            results.Add(new ValidationResult("FP-4.4.4", "At least one DatiRiepilogo is required.", ValidationSeverity.Error, "/DatiBeniServizi/DatiRiepilogo"));
        }
        else
        {
            for (var i = 0; i < dbs.DatiRiepilogo.Count; i++)
            {
                var riepilogo = dbs.DatiRiepilogo[i];
                if (riepilogo.AliquotaIVA == 0m && string.IsNullOrWhiteSpace(riepilogo.Natura))
                    results.Add(new ValidationResult("FP-4.4.5", $"DatiRiepilogo {i + 1}: when AliquotaIVA=0, Natura is required.", ValidationSeverity.Error, $"/DatiBeniServizi/DatiRiepilogo[{i + 1}]/Natura"));
            }
        }
    }

    private static FormatValidationResult BuildResult(List<ValidationResult> results)
    {
        var hasErrors = results.Any(r => r.Severity == ValidationSeverity.Error);
        return new FormatValidationResult(!hasErrors, results, InvoiceFormat.FatturaPA);
    }

    private static T DeserializeFromStream<T>(Stream stream) where T : class
    {
        var serializer = new XmlSerializer(typeof(T));
        using var reader = new StreamReader(stream, leaveOpen: true);
        return (T)serializer.Deserialize(reader)!;
    }
}
