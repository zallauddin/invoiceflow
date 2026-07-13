namespace InvoiceFlow.Infrastructure.Compliance.India.Models;

/// <summary>
/// Configuration for connecting to the Indian IRP (Invoice Registration Portal) / GSTN e-Invoice API.
/// </summary>
public class IndiaIrpConfig
{
    /// <summary>Base URL of the IRP web service endpoint.</summary>
    public string ApiBaseUrl { get; set; } = string.Empty;

    /// <summary>GSTIN (Goods and Services Tax Identification Number) of the seller.</summary>
    public string GstIn { get; set; } = string.Empty;

    /// <summary>Client ID for API authentication.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>Client secret for API authentication.</summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>Whether to use the IRP sandbox (testing) environment.</summary>
    public bool SandboxMode { get; set; } = true;
}
