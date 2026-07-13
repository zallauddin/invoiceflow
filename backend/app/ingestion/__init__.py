"""Ingestion modules — multi-source invoice intake."""

from app.ingestion.email import EmailIngestion
from app.ingestion.ftp import FTPIngestion, SFTPIngestion
from app.ingestion.storage import StorageManager

__all__ = ["EmailIngestion", "FTPIngestion", "SFTPIngestion", "StorageManager"]
