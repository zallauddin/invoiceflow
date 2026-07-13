namespace InvoiceFlow.Infrastructure.Compliance.Brazil.Models;

/// <summary>
/// Represents the complete structure of a Brazilian NF-e (Nota Fiscal Eletrônica) document.
/// </summary>
public class BrazilNfeXml
{
    /// <summary>NF-e sequential number (nNF).</summary>
    public string NfeNumber { get; set; } = string.Empty;

    /// <summary>Issue date of the NF-e (dhEmi).</summary>
    public DateTime NfeDate { get; set; }

    /// <summary>CNPJ of the emitting party (supplier).</summary>
    public string CnpjEmitente { get; set; } = string.Empty;

    /// <summary>CNPJ of the receiving party (buyer).</summary>
    public string CnpjDestinatario { get; set; } = string.Empty;

    /// <summary>IE (Inscrição Estadual) — state registration of the emitter.</summary>
    public string StateRegistration { get; set; } = string.Empty;

    /// <summary>Total NF-e value (vNF) — sum of all item totals.</summary>
    public decimal TotalNfe { get; set; }

    /// <summary>ICMS base amount (vBC).</summary>
    public decimal IcmsBase { get; set; }

    /// <summary>ICMS total value (vICMS).</summary>
    public decimal IcmsValue { get; set; }

    /// <summary>PIS total value (vPIS).</summary>
    public decimal PisValue { get; set; }

    /// <summary>COFINS total value (vCOFINS).</summary>
    public decimal CofinsValue { get; set; }

    /// <summary>Line items on the NF-e.</summary>
    public List<BrazilNfeItem> Items { get; set; } = new();
}
