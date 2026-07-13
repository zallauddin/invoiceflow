"""MinIO / S3 storage manager for invoice documents."""

import uuid
from datetime import datetime
from io import BytesIO
from pathlib import PurePosixPath

from minio import Minio
from minio.error import S3Error

from app.config import settings


class StorageManager:
    """Manages document storage in MinIO (S3-compatible)."""

    def __init__(self):
        self.client = Minio(
            settings.MINIO_ENDPOINT,
            access_key=settings.MINIO_ACCESS_KEY,
            secret_key=settings.MINIO_SECRET_KEY,
            secure=settings.MINIO_SECURE,
        )
        self.bucket = settings.MINIO_BUCKET
        self._ensure_bucket()

    def _ensure_bucket(self):
        """Create the bucket if it doesn't exist."""
        if not self.client.bucket_exists(self.bucket):
            self.client.make_bucket(self.bucket)

    def _build_path(self, tenant_id: str, category: str, filename: str) -> str:
        """Build a structured object path: {tenant}/{category}/{YYYY}/{MM}/{uuid}_{filename}"""
        now = datetime.utcnow()
        safe_filename = filename.replace(" ", "_").replace("/", "_")
        unique_id = uuid.uuid4().hex[:12]
        return str(
            PurePosixPath(str(tenant_id))
            / category
            / str(now.year)
            / f"{now.month:02d}"
            / f"{unique_id}_{safe_filename}"
        )

    async def upload_file(
        self,
        tenant_id: str,
        content: bytes,
        filename: str,
        category: str = "invoices",
        content_type: str = "application/octet-stream",
    ) -> str:
        """Upload a file and return the object path."""
        object_path = self._build_path(tenant_id, category, filename)
        data = BytesIO(content)
        self.client.put_object(
            self.bucket,
            object_path,
            data,
            length=len(content),
            content_type=content_type,
        )
        return object_path

    async def download_file(self, object_path: str) -> bytes:
        """Download a file by its object path."""
        try:
            response = self.client.get_object(self.bucket, object_path)
            return response.read()
        finally:
            response.close()
            response.release_conn()

    async def delete_file(self, object_path: str) -> bool:
        """Delete a file by its object path."""
        try:
            self.client.remove_object(self.bucket, object_path)
            return True
        except S3Error:
            return False

    async def get_presigned_url(self, object_path: str, expiry_seconds: int = 3600) -> str:
        """Generate a presigned download URL."""
        return self.client.presigned_get_object(
            self.bucket,
            object_path,
            expires=expiry_seconds,
        )

    def get_file_url(self, object_path: str) -> str:
        """Return a direct URL to the object (for internal use)."""
        protocol = "https" if settings.MINIO_SECURE else "http"
        return f"{protocol}://{settings.MINIO_ENDPOINT}/{self.bucket}/{object_path}"
