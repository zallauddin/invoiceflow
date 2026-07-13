namespace InvoiceFlow.Infrastructure.Compliance.France.Models;

/// <summary>
/// Configuration for the French PPF (Portail Public de Facturation) compliance integration.
/// </summary>
public sealed class FrancePpfConfig
{
    /// <summary>Configuration section name for binding.</summary>
    public const string SectionName = "FrancePpf";

    /// <summary>Base URL of the PPF API endpoint.</summary>
    public string ApiBaseUrl { get; set; } = string.Empty;

    /// <summary>SIRET (Système d'Identification du Répertoire des Établissements) number.</summary>
    public string Siret { get; set; } = string.Empty;

    /// <summary>VAT registration number (FR + 11 digits).</summary>
    public string VatNumber { get; set; } = string.Empty;

    /// <summary>When <c>true</c>, uses the PPF sandbox environment for testing.</summary>
    public bool SandboxMode { get; set; } = true;

    /// <summary>OAuth 2.0 client ID for PPF API authentication.</summary>
    public string OAuthClientId { get; set; } = string.Empty;

    /// <summary>OAuth 2.0 client secret for PPF API authentication.</summary>
    public string OAuthClientSecret { get; set; } = string.Empty;
}
