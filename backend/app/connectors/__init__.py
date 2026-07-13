"""ERP connectors for InvoiceFlow integration."""

from app.connectors.base import BaseERPConnector, ConnectorType, ERPInvoice, SyncDirection, SyncResult
from app.connectors.sap import SAPConnector
from app.connectors.oracle import OracleConnector
from app.connectors.xero import XeroConnector
from app.connectors.registry import get_connector, list_connectors, CONNECTOR_REGISTRY

__all__ = [
    "BaseERPConnector",
    "ConnectorType",
    "ERPInvoice",
    "SyncDirection",
    "SyncResult",
    "SAPConnector",
    "OracleConnector",
    "XeroConnector",
    "get_connector",
    "list_connectors",
    "CONNECTOR_REGISTRY",
]
