namespace InvoiceFlow.Infrastructure.Compliance.Brazil.Models;

/// <summary>
/// NF-e constants per the Brazilian fiscal portal specification (Nota Fiscal Eletrônica v4.00).
/// </summary>
public static class BrazilNfeConstants
{
    /// <summary>NFe schema version identifier.</summary>
    public const string NfeVersion = "4.00";

    /// <summary>NFe XML namespace per the Portal Fiscal Brasileiro.</summary>
    public const string NfeNamespace = "http://www.portalfiscal.inf.br/nfe";

    /// <summary>Production environment identifier (tpAmb = 1).</summary>
    public const int Producao = 1;

    /// <summary>Homologação (sandbox) environment identifier (tpAmb = 2).</summary>
    public const int Homologacao = 2;

    /// <summary>Standard ICMS rate percentage (simplified for sandbox).</summary>
    public const decimal IcmsStandardRate = 18.0m;

    /// <summary>Standard PIS rate percentage.</summary>
    public const decimal PisRate = 1.65m;

    /// <summary>Standard COFINS rate percentage.</summary>
    public const decimal CofinsRate = 7.60m;

    /// <summary>XML declaration encoding used in NF-e documents.</summary>
    public const string Encoding = "UTF-8";

    /// <summary>Transaction type: saída (outbound/sales).</summary>
    public const int TipoSaida = 1;

    /// <summary>Transaction type: entrada (inbound/purchase).</summary>
    public const int TipoEntrada = 0;
}
