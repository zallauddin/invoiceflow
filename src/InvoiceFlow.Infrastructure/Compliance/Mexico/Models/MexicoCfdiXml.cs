namespace InvoiceFlow.Infrastructure.Compliance.Mexico.Models;

/// <summary>
/// Represents the complete structure of a Mexican CFDI 4.0 (Comprobante Fiscal Digital por Internet) document.
/// </summary>
public class MexicoCfdiXml
{
    /// <summary>UUID (Fiscal ID) — assigned by the PAC after stamping.</summary>
    public string Uuid { get; set; } = string.Empty;

    /// <summary>Issue date/time of the CFDI (Fecha).</summary>
    public DateTime Fecha { get; set; }

    /// <summary>RFC of the issuing party.</summary>
    public string RfcEmisor { get; set; } = string.Empty;

    /// <summary>Legal name of the issuing party.</summary>
    public string NombreEmisor { get; set; } = string.Empty;

    /// <summary>RegimenFiscal — tax regime code of the emitter (e.g., "601" for General Law).</summary>
    public string RegimenFiscal { get; set; } = "601";

    /// <summary>RFC of the receiving party.</summary>
    public string RfcReceptor { get; set; } = string.Empty;

    /// <summary>Legal name of the receiving party.</summary>
    public string NombreReceptor { get; set; } = string.Empty;

    /// <summary>UsoCFDI — intended use code (e.g., "G03" for general expenses).</summary>
    public string UsoCfdi { get; set; } = "G03";

    /// <summary>Subtotal before taxes.</summary>
    public decimal SubTotal { get; set; }

    /// <summary>Total amount (SubTotal + Impuestos trasladados - Retenciones).</summary>
    public decimal Total { get; set; }

    /// <summary>Total IVA 16% traslado amount.</summary>
    public decimal ImpuestoTrasladoIva16 { get; set; }

    /// <summary>Currency code (ISO 4217, 3 letters).</summary>
    public string Moneda { get; set; } = "MXN";

    /// <summary>CFDI type: I = Ingreso (income/sales), E = Egreso (expense/credit note).</summary>
    public string TipoCfdi { get; set; } = "I";

    /// <summary>Payment method: PUE = una sola exhibición, PPD = parcialidades.</summary>
    public string MetodoPago { get; set; } = "PUE";

    /// <summary>Payment form: 01 = efectivo, 03 = transferencia, etc.</summary>
    public string FormaPago { get; set; } = "03";

    /// <summary>Concepto (line item) list.</summary>
    public List<MexicoCfdiConcepto> Conceptos { get; set; } = new();
}
