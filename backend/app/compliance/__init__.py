"""Compliance module — unified compliance engine.

Supports:
- PEPPOL (UBL 2.1, AS4 transmission)
- Clearance (ZATCA, Brazil NFe, India IRP, Mexico CFDI)
- CTC (Italy SdI, France PPF, Poland KSeF)
- Post-Audit (archival for audit-on-demand jurisdictions)
"""

from app.compliance.engine import ComplianceEngine, ComplianceResult

__all__ = [
    "ComplianceEngine",
    "ComplianceResult",
]
