"""Post-Audit compliant archival module.

Post-audit model: Invoices are stored locally and transmitted on demand
when requested by the tax authority during an audit. No real-time submission required.

Countries using post-audit:
- Australia (ABN/BAS reporting)
- Canada (CRA GST/HST)
- Japan (Qualified Invoice System)
- Singapore (GST)
- South Korea (Tax Invoice)
- Switzerland
"""

import hashlib
import json
import logging
import uuid
from datetime import datetime
from dataclasses import dataclass, field
from typing import Any
from pathlib import Path

logger = logging.getLogger(__name__)


@dataclass
class ArchiveRequest:
    """Data for archival."""
    invoice_number: str
    invoice_date: str
    vendor_name: str
    vendor_tax_id: str
    buyer_name: str
    buyer_tax_id: str
    currency: str
    total_amount: float
    tax_amount: float
    country_code: str
    invoice_xml: str = ""
    invoice_pdf: bytes = b""
    lines: list[dict[str, Any]] = field(default_factory=list)
    retention_years: int = 10  # Default retention period
    extra_fields: dict[str, Any] = field(default_factory=dict)


@dataclass
class ArchiveResult:
    """Result of archival."""
    success: bool = False
    archive_id: str = ""
    archive_path: str = ""
    checksum: str = ""
    retention_until: str = ""
    timestamp: str = ""
    errors: list[str] = field(default_factory=list)


class PostAuditArchiver:
    """
    Compliant archival system for post-audit jurisdictions.

    Features:
    - SHA-256 checksum for tamper evidence
    - Structured archive path: /{country}/{year}/{month}/{invoice_number}/
    - Configurable retention period (default 10 years)
    - Audit log entries for all archival actions
    - Supports multiple storage backends (local filesystem, MinIO, etc.)
    """

    def __init__(self, storage_path: str = "/data/archives"):
        self.storage_path = Path(storage_path)

    async def archive(self, request: ArchiveRequest) -> ArchiveResult:
        """
        Archive an invoice for post-audit compliance.

        Creates a structured archive with:
        - Original invoice XML/PDF
        - Metadata JSON
        - Checksum manifest
        """
        logger.info(f"Archiving invoice {request.invoice_number} for {request.country_code}")

        archive_id = f"ARCH-{uuid.uuid4().hex[:16].upper()}"
        archive_path = self._build_archive_path(request)

        try:
            # Create archive directory
            full_path = self.storage_path / archive_path
            full_path.mkdir(parents=True, exist_ok=True)

            # Write invoice XML (if provided)
            if request.invoice_xml:
                xml_file = full_path / "invoice.xml"
                xml_file.write_text(request.invoice_xml, encoding="utf-8")

            # Write invoice PDF (if provided)
            if request.invoice_pdf:
                pdf_file = full_path / "invoice.pdf"
                pdf_file.write_bytes(request.invoice_pdf)

            # Write metadata
            metadata = {
                "archive_id": archive_id,
                "invoice_number": request.invoice_number,
                "invoice_date": request.invoice_date,
                "vendor_name": request.vendor_name,
                "vendor_tax_id": request.vendor_tax_id,
                "buyer_name": request.buyer_name,
                "buyer_tax_id": request.buyer_tax_id,
                "currency": request.currency,
                "total_amount": request.total_amount,
                "tax_amount": request.tax_amount,
                "country_code": request.country_code,
                "archived_at": datetime.utcnow().isoformat() + "Z",
                "retention_until": self._calculate_retention_date(request.retention_years),
                "retention_years": request.retention_years,
                "archive_id": archive_id,
                "lines_count": len(request.lines),
            }
            meta_file = full_path / "metadata.json"
            meta_file.write_text(json.dumps(metadata, indent=2, default=str), encoding="utf-8")

            # Generate checksum manifest
            checksum = self._generate_checksum(full_path)
            manifest = {
                "archive_id": archive_id,
                "checksum_algorithm": "SHA-256",
                "checksum": checksum,
                "files": self._list_archive_files(full_path),
                "generated_at": datetime.utcnow().isoformat() + "Z",
            }
            manifest_file = full_path / "manifest.json"
            manifest_file.write_text(json.dumps(manifest, indent=2), encoding="utf-8")

            return ArchiveResult(
                success=True,
                archive_id=archive_id,
                archive_path=str(full_path),
                checksum=checksum,
                retention_until=metadata["retention_until"],
                timestamp=datetime.utcnow().isoformat() + "Z",
            )

        except Exception as e:
            logger.error(f"Archival failed for {request.invoice_number}: {e}")
            return ArchiveResult(
                success=False,
                archive_id=archive_id,
                errors=[f"Archival error: {str(e)}"],
                timestamp=datetime.utcnow().isoformat() + "Z",
            )

    async def retrieve(self, archive_id: str) -> dict[str, Any] | None:
        """
        Retrieve archived invoice data by archive ID.

        Searches the archive directory structure for the given ID.
        """
        logger.info(f"Retrieving archive {archive_id}")

        # Search for manifest files containing the archive_id
        for manifest_path in self.storage_path.rglob("manifest.json"):
            try:
                manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
                if manifest.get("archive_id") == archive_id:
                    archive_dir = manifest_path.parent
                    return {
                        "archive_id": archive_id,
                        "manifest": manifest,
                        "metadata": json.loads((archive_dir / "metadata.json").read_text(encoding="utf-8")),
                        "archive_path": str(archive_dir),
                        "has_xml": (archive_dir / "invoice.xml").exists(),
                        "has_pdf": (archive_dir / "invoice.pdf").exists(),
                    }
            except Exception:
                continue

        return None

    async def verify_integrity(self, archive_id: str) -> bool:
        """Verify archive integrity by re-computing checksum."""
        data = await self.retrieve(archive_id)
        if not data:
            return False

        archive_dir = Path(data["archive_path"])
        current_checksum = self._generate_checksum(archive_dir)
        stored_checksum = data["manifest"].get("checksum", "")

        if current_checksum != stored_checksum:
            logger.warning(f"Integrity check FAILED for {archive_id}")
            return False

        logger.info(f"Integrity check PASSED for {archive_id}")
        return True

    def _build_archive_path(self, request: ArchiveRequest) -> str:
        """Build structured archive path: /{country}/{year}/{month}/{invoice_number}/"""
        try:
            date = datetime.strptime(request.invoice_date, "%Y-%m-%d") if request.invoice_date else datetime.utcnow()
        except ValueError:
            date = datetime.utcnow()

        safe_number = "".join(c for c in request.invoice_number if c.isalnum() or c in "-_")[:50]
        return f"{request.country_code.upper()}/{date.year}/{date.month:02d}/{safe_number}"

    def _calculate_retention_date(self, years: int) -> str:
        """Calculate retention expiry date."""
        from dateutil.relativedelta import relativedelta
        try:
            expiry = datetime.utcnow() + relativedelta(years=years)
        except ImportError:
            from datetime import timedelta
            expiry = datetime.utcnow() + timedelta(days=years * 365)
        return expiry.strftime("%Y-%m-%d")

    @staticmethod
    def _generate_checksum(path: Path) -> str:
        """Generate SHA-256 checksum of all files in a directory."""
        hasher = hashlib.sha256()
        for file_path in sorted(path.rglob("*")):
            if file_path.is_file():
                hasher.update(file_path.read_bytes())
        return hasher.hexdigest()

    @staticmethod
    def _list_archive_files(path: Path) -> list[str]:
        """List all files in the archive directory."""
        return [str(f.relative_to(path)) for f in sorted(path.rglob("*")) if f.is_file()]
