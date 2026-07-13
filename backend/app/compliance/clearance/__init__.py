"""Clearance compliance module.

Provides country-specific clearance implementations:
- Saudi Arabia ZATCA FATOORAH
- Brazil NFe (SEFAZ)
- India IRP (GSTN)
- Mexico CFDI (SAT/PAC)
"""

from app.compliance.clearance.base import BaseClearance, ClearanceRequest, ClearanceResult
from app.compliance.clearance.zatca import ZATCAClearance
from app.compliance.clearance.brazil import BrazilNFeClearance
from app.compliance.clearance.india import IndiaIRPClearance
from app.compliance.clearance.mexico import MexicoCFDIClearance

# Registry for easy lookup
CLEARANCE_HANDLERS: dict[str, type[BaseClearance]] = {
    "SA": ZATCAClearance,
    "BR": BrazilNFeClearance,
    "IN": IndiaIRPClearance,
    "MX": MexicoCFDIClearance,
}

__all__ = [
    "BaseClearance",
    "ClearanceRequest",
    "ClearanceResult",
    "ZATCAClearance",
    "BrazilNFeClearance",
    "IndiaIRPClearance",
    "MexicoCFDIClearance",
    "CLEARANCE_HANDLERS",
]
