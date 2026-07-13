"""Celery application configuration."""

from celery import Celery

from app.config import settings

celery_app = Celery(
    "invoiceflow",
    broker=settings.CELERY_BROKER_URL,
    backend=settings.CELERY_RESULT_BACKEND,
)

celery_app.conf.update(
    task_serializer="json",
    accept_content=["json"],
    result_serializer="json",
    timezone="UTC",
    enable_utc=True,
    task_track_started=True,
    task_acks_late=True,
    worker_prefetch_multiplier=1,
)

celery_app.autodiscover_tasks(["app.tasks"], force=True)
celery_app.conf.update(
    include=[
        "app.tasks.ingestion_tasks",
        "app.tasks.extraction_tasks",
        "app.tasks.compliance_tasks",
        "app.tasks.erp_tasks",
        "app.tasks.webhook_tasks",
    ],
)
