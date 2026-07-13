namespace InvoiceFlow.Infrastructure.Compliance.Brazil.Models;

/// <summary>
/// Configuration for connecting to the Brazilian SEFAZ (Secretaria da Fazenda) web services.
/// </summary>
public class BrazilSefazConfig
{
    /// <summary>Base URL of the SEFAZ web service endpoint.</summary>
    public string ApiBaseUrl { get; set; } = string.Empty;

    /// <summary>CNPJ (Cadastro Nacional da Pessoa Jurídica) of the emitting company.</summary>
    public string Cnpj { get; set; } = string.Empty;

    /// <summary>Certificate thumbprint for digital signing (A1 or A3 certificate).</summary>
    public string CertificateThumbprint { get; set; } = string.Empty;

    /// <summary>Whether to use the SEFAZ homologação (sandbox) environment.</summary>
    public bool SandboxMode { get; set; } = true;

    /// <summary>UF (Unidade Federativa) state code — 2-character IBGE code (e.g., "35" for SP).</summary>
    public string StateCode { get; set; } = string.Empty;
}
