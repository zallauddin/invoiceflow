namespace InvoiceFlow.Core.Enums;

/// <summary>Type of change made in a document version.</summary>
public enum DocumentVersionChangeType
{
    /// <summary>Initial document creation.</summary>
    Created = 0,

    /// <summary>Document content updated (re-uploaded).</summary>
    Updated = 1,

    /// <summary>Document metadata changed (tags, folder, etc.).</summary>
    MetadataChanged = 2,

    /// <summary>OCR text extracted or updated.</summary>
    OcrExtracted = 3,

    /// <summary>Document linked to a business entity.</summary>
    Linked = 4,

    /// <summary>Document unlinked from a business entity.</summary>
    Unlinked = 5,

    /// <summary>Document moved to a different folder.</summary>
    Moved = 6,

    /// <summary>Document tags updated.</summary>
    TagsUpdated = 7,

    /// <summary>Document status changed.</summary>
    StatusChanged = 8,

    /// <summary>Document restored from a previous version.</summary>
    Restored = 9,

    /// <summary>Document archived.</summary>
    Archived = 10,

    /// <summary>Document deleted (soft delete).</summary>
    Deleted = 11
}