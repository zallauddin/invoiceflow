namespace InvoiceFlow.Core.Enums;

/// <summary>Compliance model for e-invoicing in a specific country.</summary>
public enum ComplianceModel
{
    Peppol = 0,
    Zatca = 1,
    BrazilNfe = 2,
    IndiaIrp = 3,
    MexicoCfdi = 4,
    ItalySdi = 5,
    FrancePpf = 6,
    PolandKsef = 7,
    PostAudit = 8
}
