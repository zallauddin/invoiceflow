"""Ingestion processing — converts ingested documents into Invoice records."""

import logging
import uuid
from datetime import datetime

from sqlalchemy import select
from sqlalchemy.ext.asyncio import AsyncSession

from app.config import settings
from app.ingestion.base import IngestedDocument
from app.ingestion.storage import StorageManager
from app.models.invoice import Invoice, InvoiceStatus, IngestionSource
from app.models.compliance import ComplianceConfig
from app.models.audit import AuditLog

logger = logging.getLogger(__name__)

# Lazy-init storage manager
_storage: StorageManager | None = None


def get_storage() -> StorageManager:
    global _storage
    if _storage is None:
        _storage = StorageManager()
    return _storage


async def determine_country_and_compliance(
    db: AsyncSession, tenant_id: uuid.UUID, filename: str
) -> tuple[str, str]:
    """Determine country code and compliance model from tenant config.

    Falls back to tenant defaults or global defaults.
    """
    result = await db.execute(
        select(ComplianceConfig).where(
            ComplianceConfig.tenant_id == tenant_id,
            ComplianceConfig.enabled == True,
        )
    )
    configs = result.scalars().all()

    if configs:
        # Return the first active config
        return configs[0].country_code, configs[0].compliance_model.value

    # Defaults
    return "US", "peppol"


async def process_ingested_document(
    db: AsyncSession,
    tenant_id: uuid.UUID,
    document: IngestedDocument,
    source: IngestionSource,
) -> Invoice:
    """Process a single ingested document into an Invoice record.

    1. Upload file to MinIO storage
    2. Create Invoice record with RECEIVED status
    3. Log the ingestion event
    4. Return the invoice (caller triggers extraction task)
    """
    storage = get_storage()

    # 1. Upload to storage
    object_path = await storage.upload_file(
        tenant_id=str(tenant_id),
        content=document.content,
        filename=document.filename,
        category="invoices",
        content_type=document.mime_type,
    )

    file_url = storage.get_file_url(object_path)

    # 2. Determine compliance
    country_code, compliance_model = await determine_country_and_compliance(
        db, tenant_id, document.filename
    )

    # 3. Create invoice record
    invoice = Invoice(
        id=uuid.uuid4(),
        tenant_id=tenant_id,
        status=InvoiceStatus.RECEIVED,
        invoice_number=f"ING-{datetime.utcnow().strftime('%Y%m%d')}-{uuid.uuid4().hex[:8].upper()}",
        vendor_name="Pending extraction",
        country_code=country_code,
        compliance_model=compliance_model,
        source=source,
        source_reference=document.source_reference,
        file_url=file_url,
        original_filename=document.filename,
        mime_type=document.mime_type,
        created_at=datetime.utcnow(),
        updated_at=datetime.utcnow(),
    )
    db.add(invoice)

    # 4. Create audit log
    audit = AuditLog(
        tenant_id=tenant_id,
        invoice_id=invoice.id,
        action="ingestion",
        details={
            "source": source.value,
            "filename": document.filename,
            "mime_type": document.mime_type,
            "storage_path": object_path,
            "source_reference": document.source_reference,
        },
        message=f"Document ingested via {source.value}",
        timestamp=datetime.utcnow(),
    )
    db.add(audit)

    await db.flush()

    logger.info(
        f"Invoice created: {invoice.id} | source={source.value} | "
        f"file={document.filename} | tenant={tenant_id}"
    )

    return invoice


async def batch_process_documents(
    db: AsyncSession,
    tenant_id: uuid.UUID,
    documents: list[IngestedDocument],
    source: IngestionSource,
) -> list[Invoice]:
    """Process multiple documents in a batch."""
    invoices = []
    for doc in documents:
        try:
            invoice = await process_ingested_document(db, tenant_id, doc, source)
            invoices.append(invoice)
        except Exception as e:
            logger.error(
                f"Failed to process document {doc.filename}: {e}",
                exc_info=True,
            )
            continue

    await db.commit()
    logger.info(
        f"Batch complete: {len(invoices)}/{len(documents)} documents processed"
    )
    return invoices
