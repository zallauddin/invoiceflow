"""Ingestion API routes — file upload, webhook, poll triggers, source management."""

import base64
import logging

from fastapi import APIRouter, Depends, HTTPException, UploadFile, File, status
from sqlalchemy import select, func
from sqlalchemy.ext.asyncio import AsyncSession

from app.api.deps import CurrentUser, get_current_user
from app.database import get_db
from app.models.invoice import Invoice, InvoiceStatus, IngestionSource
from app.schemas.ingestion import (
    FileUploadResponse,
    IngestionStatsResponse,
    PollResponse,
    WebhookPayload,
)
from app.tasks.ingestion_tasks import (
    poll_email_inbox,
    poll_ftp_folder,
    poll_sftp_folder,
    process_uploaded_file,
    process_webhook_file,
)

logger = logging.getLogger(__name__)
router = APIRouter()


# ─── File Upload ────────────────────────────────────────────────────

@router.post("/upload", response_model=FileUploadResponse)
async def upload_invoice(
    file: UploadFile = File(...),
    current_user: CurrentUser = Depends(get_current_user),
    db: AsyncSession = Depends(get_db),
):
    """Upload an invoice file for processing.

    Accepts PDF, XML, images (PNG/JPEG/TIFF). The file is stored in MinIO,
    an Invoice record is created, and extraction is queued asynchronously.
    """
    # Validate file type
    allowed_types = {
        "application/pdf", "image/png", "image/jpeg", "image/tiff",
        "application/xml", "text/xml",
    }
    content_type = file.content_type or "application/octet-stream"

    if content_type not in allowed_types:
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail=f"Unsupported file type: {content_type}. "
                   f"Allowed: {', '.join(sorted(allowed_types))}",
        )

    # Read and validate file size (max 20MB)
    content = await file.read()
    if len(content) > 20 * 1024 * 1024:
        raise HTTPException(
            status_code=status.HTTP_413_REQUEST_ENTITY_TOO_LARGE,
            detail="File too large. Maximum size is 20MB.",
        )

    # Queue async processing
    content_b64 = base64.b64encode(content).decode("utf-8")
    task = process_uploaded_file.delay(
        tenant_id=str(current_user.tenant_id),
        file_content_b64=content_b64,
        filename=file.filename or "unknown.pdf",
        mime_type=content_type,
    )

    logger.info(
        f"Upload queued: {file.filename} | task={task.id} | "
        f"tenant={current_user.tenant_id}"
    )

    return FileUploadResponse(
        invoice_id=None,  # Actual invoice created async in Celery task; use task_id for tracking
        status=InvoiceStatus.RECEIVED,
        filename=file.filename or "unknown",
        message=f"File uploaded and queued for processing. Task ID: {task.id}",
    )


# ─── Webhook Endpoint ──────────────────────────────────────────────

@router.post("/webhook", response_model=FileUploadResponse)
async def webhook_receive(
    payload: WebhookPayload,
    current_user: CurrentUser = Depends(get_current_user),
    db: AsyncSession = Depends(get_db),
):
    """Receive an invoice via webhook from an external system.

    Expects a JSON body with base64-encoded file content.
    Useful for ERP integrations, document management systems, etc.
    """
    # Validate base64 content
    try:
        file_content = base64.b64decode(payload.file_content_b64)
    except Exception:
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail="Invalid base64 content in file_content_b64",
        )

    if len(file_content) > 20 * 1024 * 1024:
        raise HTTPException(
            status_code=status.HTTP_413_REQUEST_ENTITY_TOO_LARGE,
            detail="File too large. Maximum size is 20MB.",
        )

    # Queue processing
    task = process_webhook_file.delay(
        tenant_id=str(current_user.tenant_id),
        file_content_b64=payload.file_content_b64,
        filename=payload.filename,
        mime_type=payload.mime_type,
        source_reference=payload.source_reference,
    )

    logger.info(
        f"Webhook received: {payload.filename} | task={task.id} | "
        f"tenant={current_user.tenant_id}"
    )

    return FileUploadResponse(
        invoice_id=None,  # Actual invoice created async in Celery task; use task_id for tracking
        status=InvoiceStatus.RECEIVED,
        filename=payload.filename,
        message=f"Webhook received and queued for processing. Task ID: {task.id}",
    )


# ─── Poll Triggers ─────────────────────────────────────────────────

@router.post("/poll/email", response_model=PollResponse)
async def trigger_email_poll(
    current_user: CurrentUser = Depends(get_current_user),
):
    """Trigger an email inbox poll for the current tenant.

    Checks for new emails with invoice attachments, processes them,
    and creates Invoice records.
    """
    task = poll_email_inbox.delay(tenant_id=str(current_user.tenant_id))
    return PollResponse(
        task_id=task.id,
        source_type=IngestionSource.EMAIL,
        status="queued",
        message="Email poll initiated",
    )


@router.post("/poll/ftp", response_model=PollResponse)
async def trigger_ftp_poll(
    current_user: CurrentUser = Depends(get_current_user),
):
    """Trigger an FTP folder poll for the current tenant."""
    task = poll_ftp_folder.delay(tenant_id=str(current_user.tenant_id))
    return PollResponse(
        task_id=task.id,
        source_type=IngestionSource.FTP,
        status="queued",
        message="FTP poll initiated",
    )


@router.post("/poll/sftp", response_model=PollResponse)
async def trigger_sftp_poll(
    current_user: CurrentUser = Depends(get_current_user),
):
    """Trigger an SFTP folder poll for the current tenant."""
    task = poll_sftp_folder.delay(tenant_id=str(current_user.tenant_id))
    return PollResponse(
        task_id=task.id,
        source_type=IngestionSource.SFTP,
        status="queued",
        message="SFTP poll initiated",
    )


# ─── Stats & Status ────────────────────────────────────────────────

@router.get("/stats", response_model=IngestionStatsResponse)
async def get_ingestion_stats(
    current_user: CurrentUser = Depends(get_current_user),
    db: AsyncSession = Depends(get_db),
):
    """Get ingestion statistics for the current tenant."""
    tenant_id = current_user.tenant_id

    # Total ingested
    total_result = await db.execute(
        select(func.count(Invoice.id)).where(Invoice.tenant_id == tenant_id)
    )
    total_ingested = total_result.scalar() or 0

    # By source
    source_result = await db.execute(
        select(Invoice.source, func.count(Invoice.id))
        .where(Invoice.tenant_id == tenant_id)
        .group_by(Invoice.source)
    )
    by_source = {row[0].value: row[1] for row in source_result.all()}

    # Pending processing
    pending_result = await db.execute(
        select(func.count(Invoice.id)).where(
            Invoice.tenant_id == tenant_id,
            Invoice.status.in_([InvoiceStatus.RECEIVED, InvoiceStatus.PROCESSING]),
        )
    )
    pending = pending_result.scalar() or 0

    # Last poll (most recent created_at)
    last_result = await db.execute(
        select(func.max(Invoice.created_at)).where(Invoice.tenant_id == tenant_id)
    )
    last_poll = last_result.scalar()

    return IngestionStatsResponse(
        total_ingested=total_ingested,
        by_source=by_source,
        last_poll_at=last_poll,
        pending_processing=pending,
    )


# ─── Task Status ───────────────────────────────────────────────────

@router.get("/task/{task_id}")
async def get_task_status(
    task_id: str,
    current_user: CurrentUser = Depends(get_current_user),
):
    """Check the status of an ingestion task."""
    from app.tasks.celery_app import celery_app

    result = celery_app.AsyncResult(task_id)

    return {
        "task_id": task_id,
        "status": result.state,
        "result": str(result.result) if result.result else None,
        "traceback": result.traceback if result.failed() else None,
    }
