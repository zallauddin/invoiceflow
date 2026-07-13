using System.Xml.Serialization;

namespace InvoiceFlow.Infrastructure.Formats.FatturaPa.Models;

/// <summary>General document data (DatiGeneraliDocumento) — document type, number, date, currency.</summary>
public class DatiGeneraliDocumentoType
{
    /// <summary>Document type code: TD01=Invoice, TD02=Advance, TD04=CreditNote, TD05=DebitNote, TD06=FeeNote.</summary>
    [XmlElement("TipoDocumento")]
    public string TipoDocumento { get; set; } = "TD01";

    /// <summary>Currency code (ISO 4217, always EUR for domestic Italian invoices).</summary>
    [XmlElement("Divisa")]
    public string Divisa { get; set; } = "EUR";

    /// <summary>Document issue date (yyyy-MM-dd).</summary>
    [XmlElement("Data")]
    public DateTime Data { get; set; }

    /// <summary>Document number (mandatory, max 20 chars).</summary>
    [XmlElement("Numero")]
    public string Numero { get; set; } = string.Empty;

    /// <summary>Document amount (optional, for informational purposes).</summary>
    [XmlElement("ImportoTotaleDocumento")]
    public decimal? ImportoTotaleDocumento { get; set; }

    /// <summary>Rounding amount (optional).</summary>
    [XmlElement("Arrotondamento")]
    public decimal? Arrotondamento { get; set; }

    /// <summary>Cause / reason for the document (free text, max 1000 chars).</summary>
    [XmlElement("Causale")]
    public string? Causale { get; set; }

    /// <summary>Art.73 indicator (S = standard, no special Art.73 procedure).</summary>
    [XmlElement("Art73")]
    public string? Art73 { get; set; }
}

/// <summary>Related document reference (DatiDocumentiCorrelati) — order, contract, etc.</summary>
public class DatiDocumentiCorrelatiType
{
    /// <summary>Reference number (max 20 chars).</summary>
    [XmlElement("RiferimentoNumero")]
    public string? RiferimentoNumero { get; set; }

    /// <summary>Reference date.</summary>
    [XmlElement("RiferimentoData")]
    public DateTime? RiferimentoData { get; set; }

    /// <summary>Reference numCT (for credit notes referencing original invoices).</summary>
    [XmlElement("RiferimentoNumCT")]
    public string? RiferimentoNumCT { get; set; }

    /// <summary>Reference CUP code (public investment project code).</summary>
    [XmlElement("CUP")]
    public string? CUP { get; set; }

    /// <summary>Reference CIG code (public procurement code).</summary>
    [XmlElement("CIG")]
    public string? CIG { get; set; }
}

/// <summary>General data container (DatiGenerali) — document and related document data.</summary>
public class DatiGeneraliType
{
    /// <summary>General document data (mandatory).</summary>
    [XmlElement("DatiGeneraliDocumento")]
    public DatiGeneraliDocumentoType DatiGeneraliDocumento { get; set; } = new();

    /// <summary>Purchase order reference (optional).</summary>
    [XmlElement("DatiOrdineAcquisto")]
    public DatiDocumentiCorrelatiType? DatiOrdineAcquisto { get; set; }

    /// <summary>Contract reference (optional).</summary>
    [XmlElement("DatiContratto")]
    public DatiDocumentiCorrelatiType? DatiContratto { get; set; }

    /// <summary>Reception document reference (optional).</summary>
    [XmlElement("DatiRicezione")]
    public DatiDocumentiCorrelatiType? DatiRicezione { get; set; }

    /// <summary>Transport document reference (optional).</summary>
    [XmlElement("DatiTrasporto")]
    public DatiDocumentiCorrelatiType? DatiTrasporto { get; set; }

    /// <summary>FatturaPA reference (optional, for related invoices).</summary>
    [XmlElement("FatturaPrincipale")]
    public DatiDocumentiCorrelatiType? FatturaPrincipale { get; set; }
}

/// <summary>Goods and services data (DatiBeniServizi) — line items and tax summaries.</summary>
public class DatiBeniServiziType
{
    /// <summary>Line item details (mandatory, at least one line required).</summary>
    [XmlElement("DettaglioLinee")]
    public List<DettaglioLineaType> DettaglioLinee { get; set; } = new();

    /// <summary>Tax summary per rate/exemption (mandatory, at least one required).</summary>
    [XmlElement("DatiRiepilogo")]
    public List<DatiRiepilogoType> DatiRiepilogo { get; set; } = new();
}

/// <summary>Payment detail (DettaglioPagamento) — single payment instruction.</summary>
public class DettaglioPagamentoType
{
    /// <summary>Payment method code: MP01=Cash, MP02=Check, MP05=Wire, MP08=Card, etc.</summary>
    [XmlElement("ModalitaPagamento")]
    public string ModalitaPagamento { get; set; } = "MP05";

    /// <summary>Payment due date (optional).</summary>
    [XmlElement("DataScadenzaPagamento")]
    public DateTime? DataScadenzaPagamento { get; set; }

    /// <summary>Payment amount (mandatory).</summary>
    [XmlElement("ImportoPagamento")]
    public decimal ImportoPagamento { get; set; }

    /// <summary>Payment identifier (optional).</summary>
    [XmlElement("IdentificazionePagamento")]
    public string? IdentificazionePagamento { get; set; }
}

/// <summary>Payment data container (DatiPagamento) — payment conditions and details.</summary>
public class DatiPagamentoType
{
    /// <summary>Payment condition: TP01=Advance, TP02=Deferred, TP03=Partial, TP04=Full.</summary>
    [XmlElement("CondizioniPagamento")]
    public string CondizioniPagamento { get; set; } = "TP02";

    /// <summary>Payment details (at least one required).</summary>
    [XmlElement("DettaglioPagamento")]
    public List<DettaglioPagamentoType> DettaglioPagamento { get; set; } = new();
}

/// <summary>Attachment (Allegato) — file attachment metadata.</summary>
public class AllegatoType
{
    /// <summary>Attachment name (mandatory).</summary>
    [XmlElement("NomeAttachment")]
    public string NomeAttachment { get; set; } = string.Empty;

    /// <summary>Attachment format (e.g., "application/pdf").</summary>
    [XmlElement("FormatoAttachment")]
    public string? FormatoAttachment { get; set; }

    /// <summary>Attachment description.</summary>
    [XmlElement("DescrizioneAttachment")]
    public string? DescrizioneAttachment { get; set; }

    /// <summary>Base64-encoded attachment content.</summary>
    [XmlElement("Attachment")]
    public string? Attachment { get; set; }
}

/// <summary>FatturaElettronicaBody — invoice body with general data, goods/services, and payment.</summary>
public class FatturaElettronicaBodyType
{
    /// <summary>General document data (mandatory).</summary>
    [XmlElement("DatiGenerali")]
    public DatiGeneraliType DatiGenerali { get; set; } = new();

    /// <summary>Goods and services data (mandatory).</summary>
    [XmlElement("DatiBeniServizi")]
    public DatiBeniServiziType DatiBeniServizi { get; set; } = new();

    /// <summary>Payment data (optional but recommended).</summary>
    [XmlElement("DatiPagamento")]
    public DatiPagamentoType? DatiPagamento { get; set; }

    /// <summary>Attachments (optional).</summary>
    [XmlElement("Allegati")]
    public List<AllegatoType>? Allegati { get; set; }
}
