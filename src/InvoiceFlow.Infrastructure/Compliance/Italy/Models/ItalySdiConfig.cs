namespace InvoiceFlow.Infrastructure.Compliance.Italy.Models;

/// <summary>
/// Configuration for the Italian SdI (Sistema di Interscambio) compliance integration.
/// </summary>
public sealed class ItalySdiConfig
{
    /// <summary>Configuration section name for binding.</summary>
    public const string SectionName = "ItalySdi";

    /// <summary>Base URL of the SdI web service endpoint.</summary>
    public string ApiBaseUrl { get; set; } = string.Empty;

    /// <summary>Codice Fiscale of the transmitting entity.</summary>
    public string CodiceFiscale { get; set; } = string.Empty;

    /// <summary>Partita IVA of the transmitting entity.</summary>
    public string PartitaIva { get; set; } = string.Empty;

    /// <summary>PEC (Posta Elettronica Certificata) address for SdI communication.</summary>
    public string PecAddress { get; set; } = string.Empty;

    /// <summary>When <c>true</c>, uses the SdI sandbox environment for testing.</summary>
    public bool SandboxMode { get; set; } = true;

    /// <summary>Thumbprint of the X.509 certificate used for SdI authentication.</summary>
    public string? CertificateThumbprint { get; set; }
}
