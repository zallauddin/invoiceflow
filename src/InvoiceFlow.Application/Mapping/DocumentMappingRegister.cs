using InvoiceFlow.Core.DTOs.Documents;
using InvoiceFlow.Core.Entities;
using Mapster;

namespace InvoiceFlow.Application.Mapping;

/// <summary>Mapster type mappings between document entities and DTOs.</summary>
public sealed class DocumentMappingRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        // --- Entity → DTO mappings ---
        config.NewConfig<CreditNote, CreditNoteDto>();

        config.NewConfig<DebitNote, DebitNoteDto>();

        config.NewConfig<PurchaseOrder, PurchaseOrderDto>();

        config.NewConfig<DeliveryNote, DeliveryNoteDto>();

        config.NewConfig<Reminder, ReminderDto>();

        config.NewConfig<DocumentRelationship, DocumentRelationshipDto>();

        config.NewConfig<DocumentVersionHistory, DocumentVersionHistoryDto>();

        // --- Request → Entity mappings ---
        config.NewConfig<CreateCreditNoteRequest, CreditNote>()
            .Ignore("Id", "TenantId", "CreatedAt", "UpdatedAt",
                    "IsDeleted", "DeletedAt", "DeletedByUserId", "RowVersion")
            .Ignore("Lines", "Tenant", "OriginalInvoice");

        config.NewConfig<CreateDebitNoteRequest, DebitNote>()
            .Ignore("Id", "TenantId", "CreatedAt", "UpdatedAt",
                    "IsDeleted", "DeletedAt", "DeletedByUserId", "RowVersion")
            .Ignore("Lines", "Tenant", "OriginalInvoice");

        config.NewConfig<CreatePurchaseOrderRequest, PurchaseOrder>()
            .Ignore("Id", "TenantId", "CreatedAt", "UpdatedAt",
                    "IsDeleted", "DeletedAt", "DeletedByUserId", "RowVersion")
            .Ignore("Lines", "Tenant");

        config.NewConfig<CreateDeliveryNoteRequest, DeliveryNote>()
            .Ignore("Id", "TenantId", "CreatedAt", "UpdatedAt",
                    "IsDeleted", "DeletedAt", "DeletedByUserId", "RowVersion")
            .Ignore("Lines", "Tenant", "PurchaseOrder");

        config.NewConfig<CreateReminderRequest, Reminder>()
            .Ignore("Id", "TenantId", "CreatedAt", "UpdatedAt",
                    "IsDeleted", "DeletedAt", "DeletedByUserId", "RowVersion")
            .Ignore("Lines", "Tenant", "Invoice");
    }
}
