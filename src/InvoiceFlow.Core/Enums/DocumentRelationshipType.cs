namespace InvoiceFlow.Core.Enums;

/// <summary>Type of relationship between two documents.</summary>
public enum DocumentRelationshipType
{
    /// <summary>Source document references target document (e.g., invoice references purchase order).</summary>
    References = 0,

    /// <summary>Source document is a version of target document.</summary>
    VersionOf = 1,

    /// <summary>Source document replaces target document (supersedes).</summary>
    Replaces = 2,

    /// <summary>Source document is a correction of target document.</summary>
    Corrects = 3,

    /// <summary>Source document is a credit note for target invoice.</summary>
    CreditNoteFor = 4,

    /// <summary>Source document is a debit note for target invoice.</summary>
    DebitNoteFor = 5,

    /// <summary>Source document is a delivery note for target purchase order.</summary>
    DeliveryNoteFor = 6,

    /// <summary>Source document is a reminder for target invoice.</summary>
    ReminderFor = 7,

    /// <summary>Source document is attached to target document.</summary>
    AttachmentOf = 8,

    /// <summary>Source document supports target document (e.g., supporting evidence).</summary>
    Supports = 9,

    /// <summary>Source document is derived from target document (OCR, extraction).</summary>
    DerivedFrom = 10,

    /// <summary>Custom relationship type defined by user.</summary>
    Custom = 11
}