namespace InvoiceFlow.Infrastructure.Compliance.Italy.Models;

/// <summary>
/// SdI notification acknowledgment received when querying invoice processing status.
/// </summary>
public sealed class ItalySdiAcknowledgment
{
    /// <summary>
    /// SdI esito (outcome) code.
    /// ES01 = accepted, ES02 = rejected.
    /// </summary>
    public string Esito { get; set; } = string.Empty;

    /// <summary>Name of the FatturaPA file processed by SdI.</summary>
    public string NomeFile { get; set; } = string.Empty;

    /// <summary>Unique numeric identifier assigned by SdI to this invoice.</summary>
    public long IdSdI { get; set; }

    /// <summary>UTC date-time when SdI received the notification.</summary>
    public DateTime DataOraRicezione { get; set; }

    /// <summary>Descriptive message from SdI (e.g., "Notifica di accettazione").</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Whether the SdI outcome indicates acceptance (ES01).</summary>
    public bool IsAccepted => string.Equals(Esito, "ES01", StringComparison.OrdinalIgnoreCase);
}
