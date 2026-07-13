namespace InvoiceFlow.Infrastructure.Compliance.Mexico.Models;

/// <summary>
/// A single concepto (line item) within a Mexican CFDI 4.0 document.
/// </summary>
public class MexicoCfdiConcepto
{
    /// <summary>ClaveProdServ — SAT product/service key (8-digit catalog code).</summary>
    public string ClaveProdServ { get; set; } = string.Empty;

    /// <summary>Quantity of the product or service.</summary>
    public decimal Cantidad { get; set; }

    /// <summary>ClaveUnidad — SAT unit of measurement key (3-character UoM code).</summary>
    public string ClaveUnidad { get; set; } = "H87";

    /// <summary>Description of the product or service.</summary>
    public string Descripcion { get; set; } = string.Empty;

    /// <summary>Unit price in MXN.</summary>
    public decimal ValorUnitario { get; set; }

    /// <summary>Total amount for the line (Cantidad * ValorUnitario).</summary>
    public decimal Importe { get; set; }

    /// <summary>ObjetoImp — tax object code: 01=not subject, 02=subject to tax.</summary>
    public string ObjetoImp { get; set; } = "02";

    /// <summary>IVA 16% traslado amount for this line.</summary>
    public decimal TrasladoIva16 { get; set; }

    /// <summary>Base amount for IVA 16% calculation.</summary>
    public decimal BaseIva16 { get; set; }
}
