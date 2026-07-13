"""ERP connector registry and factory."""

from typing import Any, Dict, List, Optional, Type

from app.connectors.base import BaseERPConnector, ConnectorType, SyncDirection

# Import all connectors
from app.connectors.sap import SAPConnector
from app.connectors.oracle import OracleConnector
from app.connectors.xero import XeroConnector

# Connector registry
CONNECTOR_REGISTRY: Dict[str, Type[BaseERPConnector]] = {
    "sap": SAPConnector,
    "oracle": OracleConnector,
    "xero": XeroConnector,
}


def get_connector(
    connector_type: str,
    api_key: str = "",
    api_secret: str = "",
    base_url: str = "",
    sandbox: bool = True,
    tenant_id: str = "",
    extra_config: Optional[Dict[str, str]] = None,
) -> BaseERPConnector:
    """Factory to create an ERP connector instance."""
    connector_cls = CONNECTOR_REGISTRY.get(connector_type.lower())
    if not connector_cls:
        raise ValueError(
            f"Unknown connector type: {connector_type}. "
            f"Available: {list(CONNECTOR_REGISTRY.keys())}"
        )
    return connector_cls(
        api_key=api_key,
        api_secret=api_secret,
        base_url=base_url,
        sandbox=sandbox,
        tenant_id=tenant_id,
        extra_config=extra_config,
    )


def list_connectors() -> List[Dict[str, Any]]:
    """List all available connectors with metadata."""
    return [
        {
            "type": k,
            "display_name": v.display_name if hasattr(v, "display_name") else k.upper(),
            "directions": [d.value for d in v.supported_directions] if hasattr(v, "supported_directions") else [],
        }
        for k, v in CONNECTOR_REGISTRY.items()
    ]
