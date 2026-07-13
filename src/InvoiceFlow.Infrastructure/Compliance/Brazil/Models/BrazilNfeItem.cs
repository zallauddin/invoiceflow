namespace InvoiceFlow.Infrastructure.Compliance.Brazil.Models;

/// <summary>
/// A single line item within a Brazilian NF-e document.
/// </summary>
public class BrazilNfeItem
{
    /// <summary>Product/service code assigned by the emitter.</summary>
    public string Codigo { get; set; } = string.Empty;

    /// <summary>Product or service description.</summary>
    public string Descricao { get; set; } = string.Empty;

    /// <summary>NCM (Nomenclatura Comum do Mercosul) code — 8-digit product classification.</summary>
    public string NcmCode { get; set; } = string.Empty;

    /// <summary>CFOP (Código Fiscal de Operações e Prestações) — 4-digit tax operation code.</summary>
    public string Cfop { get; set; } = string.Empty;

    /// <summary>Unit of measurement (e.g., "UN", "KG", "LT").</summary>
    public string Unidade { get; set; } = "UN";

    /// <summary>Quantity of items.</summary>
    public decimal Quantidade { get; set; }

    /// <summary>Unit price in BRL.</summary>
    public decimal ValorUnitario { get; set; }

    /// <summary>Total value for the line (Quantidade * ValorUnitario).</summary>
    public decimal ValorTotal { get; set; }

    /// <summary>ICMS tax rate percentage applied to this item.</summary>
    public decimal IcmsRate { get; set; }

    /// <summary>ICMS tax amount in BRL.</summary>
    public decimal IcmsValue { get; set; }

    /// <summary>PIS tax rate percentage applied to this item.</summary>
    public decimal PisRate { get; set; }

    /// <summary>PIS tax amount in BRL.</summary>
    public decimal PisValue { get; set; }

    /// <summary>COFINS tax rate percentage applied to this item.</summary>
    public decimal CofinsRate { get; set; }

    /// <summary>COFINS tax amount in BRL.</summary>
    public decimal CofinsValue { get; set; }
}
