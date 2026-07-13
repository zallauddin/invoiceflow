"""Base ingestion classes — shared logic for all intake channels."""

import abc
import logging
from dataclasses import dataclass, field
from typing import Optional

logger = logging.getLogger(__name__)

# Supported invoice MIME types
SUPPORTED_MIME_TYPES = {
    "application/pdf",
    "image/png",
    "image/jpeg",
    "image/tiff",
    "image/bmp",
    "application/xml",
    "text/xml",
    "application/vnd.ms-xbrl",
    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
}

# Supported file extensions (fallback when MIME type is missing)
SUPPORTED_EXTENSIONS = {
    ".pdf", ".png", ".jpg", ".jpeg", ".tiff", ".tif", ".bmp",
    ".xml", ".xbrl", ".xlsx", ".csv",
}


@dataclass
class IngestedDocument:
    """Represents a document pulled from any ingestion source."""
    filename: str
    content: bytes
    mime_type: str = "application/octet-stream"
    source_reference: str = ""  # e.g. email message ID, FTP path, API request ID
    metadata: dict = field(default_factory=dict)


class BaseIngestion(abc.ABC):
    """Abstract base for all ingestion channels.

    Each channel (email, FTP, API) implements `poll()` and `fetch()`.
    """

    def __init__(self, tenant_id: str, source_type: str):
        self.tenant_id = tenant_id
        self.source_type = source_type
        self.logger = logging.getLogger(f"ingestion.{source_type}")

    @abc.abstractmethod
    async def poll(self) -> list[IngestedDocument]:
        """Poll the source and return new documents."""
        ...

    @abc.abstractmethod
    async def fetch(self, reference: str) -> IngestedDocument:
        """Fetch a specific document by reference."""
        ...

    @staticmethod
    def is_supported(filename: str, mime_type: str = "") -> bool:
        """Check if the file type is supported for processing."""
        if mime_type in SUPPORTED_MIME_TYPES:
            return True
        for ext in SUPPORTED_EXTENSIONS:
            if filename.lower().endswith(ext):
                return True
        return False

    @staticmethod
    def extract_extension(filename: str) -> str:
        """Extract the file extension from a filename."""
        return filename.rsplit(".", 1)[-1].lower() if "." in filename else ""
