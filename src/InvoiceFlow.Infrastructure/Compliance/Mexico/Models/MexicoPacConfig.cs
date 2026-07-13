namespace InvoiceFlow.Infrastructure.Compliance.Mexico.Models;

/// <summary>
/// Configuration for connecting to a PAC (Proveedor Autorizado de Certificación) for CFDI stamping.
/// </summary>
public class MexicoPacConfig
{
    /// <summary>Base URL of the PAC API endpoint.</summary>
    public string PacApiBaseUrl { get; set; } = string.Empty;

    /// <summary>RFC (Registro Federal de Contribuyentes) of the emitting company.</summary>
    public string Rfc { get; set; } = string.Empty;

    /// <summary>Certificate thumbprint for digital signing (CSD/e.firm).</summary>
    public string CertificateThumbprint { get; set; } = string.Empty;

    /// <summary>Whether to use the PAC sandbox (testing) environment.</summary>
    public bool SandboxMode { get; set; } = true;

    /// <summary>PAC provider name (e.g., "FINKOK", "SOFTPAY", "DIGITALFISCAL").</summary>
    public string PacProvider { get; set; } = string.Empty;
}
