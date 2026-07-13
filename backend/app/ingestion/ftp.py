"""FTP and SFTP ingestion channels — poll drop folders for invoice files."""

import ftplib
import logging
import os
from datetime import datetime
from io import BytesIO
from typing import Optional

import paramiko

from app.config import settings
from app.ingestion.base import BaseIngestion, IngestedDocument

logger = logging.getLogger(__name__)


class FTPIngestion(BaseIngestion):
    """FTP-based invoice ingestion.

    Connects to an FTP server, lists files in a designated folder,
    downloads supported invoice files, and tracks what's been processed.
    """

    # Files to skip (hidden, temporary, or lock files)
    SKIP_PATTERNS = {".", "..", ".DS_Store", "Thumbs.db"}

    def __init__(self, tenant_id: str):
        super().__init__(tenant_id, source_type="ftp")
        self.host = settings.FTP_HOST
        self.port = settings.FTP_PORT
        self.username = settings.FTP_USERNAME
        self.password = settings.FTP_PASSWORD
        self.remote_dir = os.getenv("FTP_REMOTE_DIR", "/invoices/inbox")
        self.processed_dir = os.getenv("FTP_REMOTE_PROCESSED", "/invoices/processed")
        self._processed_files: set[str] = set()

    def _connect(self) -> ftplib.FTP:
        """Establish FTP connection."""
        if not self.host:
            raise ValueError("FTP_HOST is not configured")
        ftp = ftplib.FTP()
        ftp.connect(self.host, self.port, timeout=30)
        ftp.login(self.username, self.password)
        return ftp

    def _ensure_dirs(self, ftp: ftplib.FTP):
        """Ensure remote directories exist."""
        for directory in [self.remote_dir, self.processed_dir]:
            try:
                ftp.mkd(directory)
            except ftplib.error_perm:
                pass  # Directory already exists

    def _list_files(self, ftp: ftplib.FTP, directory: str) -> list[str]:
        """List files in a remote directory."""
        try:
            ftp.cwd(directory)
            entries = []
            ftp.retrlines("LIST", entries.append)

            files = []
            for entry in entries:
                parts = entry.split()
                if len(parts) < 9:
                    continue
                filename = " ".join(parts[8:])
                if filename in self.SKIP_PATTERNS:
                    continue
                # Skip directories (first char is 'd')
                if entry.startswith("d"):
                    continue
                files.append(filename)
            return files
        except Exception as e:
            self.logger.error(f"Error listing FTP directory {directory}: {e}")
            return []

    def _download_file(self, ftp: ftplib.FTP, remote_path: str) -> bytes:
        """Download a file from FTP server."""
        buffer = BytesIO()
        ftp.retrbinary(f"RETR {remote_path}", buffer.write)
        return buffer.getvalue()

    def _move_file(self, ftp: ftplib.FTP, filename: str):
        """Move a processed file to the processed directory."""
        try:
            ftp.rename(
                f"{self.remote_dir}/{filename}",
                f"{self.processed_dir}/{filename}",
            )
        except Exception as e:
            self.logger.warning(f"Could not move {filename} to processed: {e}")

    async def poll(self) -> list[IngestedDocument]:
        """Poll FTP server for new invoice files."""
        all_documents = []
        ftp = None

        try:
            ftp = self._connect()
            self._ensure_dirs(ftp)

            files = self._list_files(ftp, self.remote_dir)
            self.logger.info(f"Found {len(files)} files in {self.remote_dir}")

            for filename in files:
                if filename in self._processed_files:
                    continue

                if not self.is_supported(filename):
                    self.logger.info(f"Skipping unsupported file: {filename}")
                    continue

                try:
                    content = self._download_file(ftp, f"{self.remote_dir}/{filename}")
                    documents = [
                        IngestedDocument(
                            filename=filename,
                            content=content,
                            mime_type=self._guess_mime(filename),
                            source_reference=f"ftp://{self.host}{self.remote_dir}/{filename}",
                            metadata={
                                "ftp_host": self.host,
                                "remote_path": f"{self.remote_dir}/{filename}",
                                "downloaded_at": datetime.utcnow().isoformat(),
                            },
                        )
                    ]
                    all_documents.extend(documents)

                    # Move to processed folder
                    self._move_file(ftp, filename)
                    self._processed_files.add(filename)

                    self.logger.info(f"Downloaded and processed: {filename}")

                except Exception as e:
                    self.logger.error(f"Error downloading {filename}: {e}")
                    continue

        except Exception as e:
            self.logger.error(f"FTP polling error: {e}")
            raise
        finally:
            if ftp:
                try:
                    ftp.quit()
                except Exception:
                    pass

        return all_documents

    async def fetch(self, reference: str) -> IngestedDocument:
        """Fetch a specific file by its FTP path reference."""
        ftp = self._connect()
        try:
            content = self._download_file(ftp, reference)
            filename = os.path.basename(reference)
            return IngestedDocument(
                filename=filename,
                content=content,
                mime_type=self._guess_mime(filename),
                source_reference=reference,
                metadata={"ftp_host": self.host, "remote_path": reference},
            )
        finally:
            ftp.quit()

    @staticmethod
    def _guess_mime(filename: str) -> str:
        """Guess MIME type from file extension."""
        ext = filename.rsplit(".", 1)[-1].lower() if "." in filename else ""
        mime_map = {
            "pdf": "application/pdf",
            "xml": "application/xml",
            "png": "image/png",
            "jpg": "image/jpeg",
            "jpeg": "image/jpeg",
            "tiff": "image/tiff",
            "tif": "image/tiff",
            "csv": "text/csv",
        }
        return mime_map.get(ext, "application/octet-stream")


class SFTPIngestion(BaseIngestion):
    """SFTP-based invoice ingestion using Paramiko.

    Similar to FTP but uses SSH/SFTP protocol for secure file transfer.
    """

    def __init__(self, tenant_id: str):
        super().__init__(tenant_id, source_type="sftp")
        self.host = settings.SFTP_HOST
        self.port = settings.SFTP_PORT
        self.username = settings.SFTP_USERNAME
        self.password = settings.SFTP_PASSWORD
        self.remote_dir = os.getenv("SFTP_REMOTE_DIR", "/invoices/inbox")
        self.processed_dir = os.getenv("SFTP_REMOTE_PROCESSED", "/invoices/processed")
        self._processed_files: set[str] = set()

    def _connect(self) -> paramiko.SFTPClient:
        """Establish SFTP connection."""
        if not self.host:
            raise ValueError("SFTP_HOST is not configured")

        transport = paramiko.Transport((self.host, self.port))
        transport.connect(username=self.username, password=self.password)
        sftp = paramiko.SFTPClient.from_transport(transport)
        return sftp

    def _ensure_dirs(self, sftp: paramiko.SFTPClient):
        """Ensure remote directories exist."""
        for directory in [self.remote_dir, self.processed_dir]:
            try:
                sftp.mkdir(directory)
            except IOError:
                pass  # Directory already exists

    def _list_files(self, sftp: paramiko.SFTPClient, directory: str) -> list[str]:
        """List files in a remote SFTP directory."""
        try:
            entries = sftp.listdir_attr(directory)
            files = []
            for entry in entries:
                if entry.filename.startswith(".") or entry.filename == "..":
                    continue
                # Skip directories
                if hasattr(entry, "st_mode") and entry.st_mode and 0o040000 == (entry.st_mode & 0o170000):
                    continue
                files.append(entry.filename)
            return files
        except Exception as e:
            self.logger.error(f"Error listing SFTP directory {directory}: {e}")
            return []

    def _download_file(self, sftp: paramiko.SFTPClient, remote_path: str) -> bytes:
        """Download a file from SFTP server."""
        with sftp.open(remote_path, "rb") as f:
            return f.read()

    def _move_file(self, sftp: paramiko.SFTPClient, filename: str):
        """Move a processed file to the processed directory."""
        try:
            sftp.rename(
                f"{self.remote_dir}/{filename}",
                f"{self.processed_dir}/{filename}",
            )
        except Exception as e:
            self.logger.warning(f"Could not move {filename} to processed: {e}")

    async def poll(self) -> list[IngestedDocument]:
        """Poll SFTP server for new invoice files."""
        all_documents = []
        sftp = None

        try:
            sftp = self._connect()
            self._ensure_dirs(sftp)

            files = self._list_files(sftp, self.remote_dir)
            self.logger.info(f"Found {len(files)} files in {self.remote_dir}")

            for filename in files:
                if filename in self._processed_files:
                    continue

                if not self.is_supported(filename):
                    self.logger.info(f"Skipping unsupported file: {filename}")
                    continue

                try:
                    content = self._download_file(sftp, f"{self.remote_dir}/{filename}")
                    documents = [
                        IngestedDocument(
                            filename=filename,
                            content=content,
                            mime_type=FTPIngestion._guess_mime(filename),
                            source_reference=f"sftp://{self.host}{self.remote_dir}/{filename}",
                            metadata={
                                "sftp_host": self.host,
                                "remote_path": f"{self.remote_dir}/{filename}",
                                "downloaded_at": datetime.utcnow().isoformat(),
                            },
                        )
                    ]
                    all_documents.extend(documents)

                    self._move_file(sftp, filename)
                    self._processed_files.add(filename)

                    self.logger.info(f"Downloaded and processed: {filename}")

                except Exception as e:
                    self.logger.error(f"Error downloading {filename}: {e}")
                    continue

        except Exception as e:
            self.logger.error(f"SFTP polling error: {e}")
            raise
        finally:
            if sftp:
                try:
                    sftp.close()
                except Exception:
                    pass

        return all_documents

    async def fetch(self, reference: str) -> IngestedDocument:
        """Fetch a specific file by its SFTP path reference."""
        sftp = self._connect()
        try:
            content = self._download_file(sftp, reference)
            filename = os.path.basename(reference)
            return IngestedDocument(
                filename=filename,
                content=content,
                mime_type=FTPIngestion._guess_mime(filename),
                source_reference=reference,
                metadata={"sftp_host": self.host, "remote_path": reference},
            )
        finally:
            sftp.close()
