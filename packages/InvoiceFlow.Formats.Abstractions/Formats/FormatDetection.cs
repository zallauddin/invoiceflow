using System.Text;

namespace InvoiceFlow.Formats.Abstractions;

/// <summary>Static helper for detecting invoice formats from file extensions and XML content.</summary>
public static class FormatDetection
{
    private static readonly Dictionary<string, InvoiceFormat> ExtensionMap = new(StringComparer.OrdinalIgnoreCase)
    {
        [".xml"] = InvoiceFormat.Unknown, // Requires namespace inspection
        [".pdf"] = InvoiceFormat.Pdf,
        [".csv"] = InvoiceFormat.Csv,
        [".p7m"] = InvoiceFormat.FatturaPA,
        [".p7c"] = InvoiceFormat.FatturaPA,
        [".factur-x.pdf"] = InvoiceFormat.Zugferd,
        [".zugferd.pdf"] = InvoiceFormat.Zugferd,
        [".idoc"] = InvoiceFormat.Idoc
    };

    private static readonly Dictionary<string, InvoiceFormat> NamespaceMap = new(StringComparer.OrdinalIgnoreCase)
    {
        // UBL 2.1 Invoice
        ["urn:oasis:names:specification:ubl:schema:xsd:Invoice-2"] = InvoiceFormat.Ubl21,
        // UBL 2.1 Credit Note
        ["urn:oasis:names:specification:ubl:schema:xsd:CreditNote-2"] = InvoiceFormat.Ubl21,
        // UN/CEFACT Cross-Industry Invoice
        ["urn:un:unece:uncefact:data:standard:CrossIndustryInvoice:100"] = InvoiceFormat.Cii,
        // Italian FatturaPA
        ["http://ivaservizi.agenziaentrate.gov.it/docs/xsd/fatture/v1.2"] = InvoiceFormat.FatturaPA,
        // Austrian ebInterface
        ["http://www.ebinterface.gv.at/namespace"] = InvoiceFormat.EbInterface,
        // Mexico CFDI 4.0
        ["http://www.sat.gob.mx/cfd/4"] = InvoiceFormat.Cfdi,
        // Brazil NF-e
        ["http://www.portalfiscal.inf.br/nfe"] = InvoiceFormat.Nfe,
    };

    private const string XRechnungCustomizationId = "urn:cen.eu:en16931:2017#compliant#urn:xoev-de:kosit:standard:xrechnung_3.0";

    /// <summary>Detect format from a file name using extension mapping.</summary>
    public static InvoiceFormat DetectFromFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return InvoiceFormat.Unknown;
        }

        var lowerFileName = fileName.ToLowerInvariant();

        // Check compound extensions first (e.g., ".factur-x.pdf", ".zugferd.pdf")
        if (lowerFileName.EndsWith(".factur-x.pdf", StringComparison.Ordinal))
        {
            return InvoiceFormat.Zugferd;
        }

        if (lowerFileName.EndsWith(".zugferd.pdf", StringComparison.Ordinal))
        {
            return InvoiceFormat.Zugferd;
        }

        var extension = Path.GetExtension(fileName);
        if (string.IsNullOrEmpty(extension))
        {
            return InvoiceFormat.Unknown;
        }

        return ExtensionMap.TryGetValue(extension, out var format) ? format : InvoiceFormat.Unknown;
    }

    /// <summary>Detect format from a stream by reading the first portion and inspecting XML namespaces.</summary>
    public static InvoiceFormat DetectFromStream(Stream stream)
    {
        if (stream == null || !stream.CanRead || stream.Length == 0)
        {
            return InvoiceFormat.Unknown;
        }

        var originalPosition = stream.Position;
        try
        {
            stream.Position = 0;

            // Read first 4KB for namespace detection — enough for root element
            var buffer = new byte[4096];
            var bytesRead = stream.Read(buffer, 0, buffer.Length);
            if (bytesRead == 0)
            {
                return InvoiceFormat.Unknown;
            }

            var content = Encoding.UTF8.GetString(buffer, 0, bytesRead);

            return DetectFromXmlContent(content);
        }
        catch (DecoderFallbackException)
        {
            // If UTF-8 decoding fails, likely not XML
            return InvoiceFormat.Unknown;
        }
        finally
        {
            stream.Position = originalPosition;
        }
    }

    /// <summary>Detect format by inspecting XML content for namespace URIs and customization IDs.</summary>
    public static InvoiceFormat DetectFromXmlContent(string xmlContent)
    {
        if (string.IsNullOrWhiteSpace(xmlContent))
        {
            return InvoiceFormat.Unknown;
        }

        // Check for XRechnung first (requires both UBL namespace and specific customization ID)
        if (xmlContent.Contains(XRechnungCustomizationId, StringComparison.OrdinalIgnoreCase))
        {
            return InvoiceFormat.XRechnung;
        }

        // Check for UBL namespace variants (XRechnung before this so it doesn't match)
        if (xmlContent.Contains("urn:oasis:names:specification:ubl:schema:xsd:Invoice-2", StringComparison.OrdinalIgnoreCase) ||
            xmlContent.Contains("urn:oasis:names:specification:ubl:schema:xsd:CreditNote-2", StringComparison.OrdinalIgnoreCase))
        {
            return InvoiceFormat.Ubl21;
        }

        // Check remaining namespace mappings
        foreach (var (ns, format) in NamespaceMap)
        {
            // Skip UBL entries (already handled above)
            if (format == InvoiceFormat.Ubl21)
            {
                continue;
            }

            if (xmlContent.Contains(ns, StringComparison.OrdinalIgnoreCase))
            {
                return format;
            }
        }

        // Check if it's XML at all
        var trimmed = xmlContent.AsSpan().TrimStart();
        if (trimmed.Length > 0 && trimmed[0] == '<')
        {
            // It's XML but we don't recognize the namespace
            return InvoiceFormat.Unknown;
        }

        return InvoiceFormat.Unknown;
    }
}
