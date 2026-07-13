"""ERP Connector Pydantic schemas."""

from datetime import datetime
from typing import Any, Dict, List, Optional
from pydantic import BaseModel


class ConnectorCreate(BaseModel):
    connector_type: str  # sap, oracle, xero
    display_name: str
    api_key: Optional[str] = None
    api_secret: Optional[str] = None
    base_url: Optional[str] = None
    sandbox: bool = True
    sync_direction: str = "push"
    extra_config: Optional[Dict[str, str]] = None


class ConnectorResponse(BaseModel):
    id: str
    connector_type: str
    display_name: str
    status: str
    sandbox: bool
    sync_direction: str
    last_sync_at: Optional[datetime] = None
    total_synced: int = 0
    total_failed: int = 0
    created_at: datetime

    class Config:
        from_attributes = True


class ConnectorSyncRequest(BaseModel):
    direction: str = "push"  # push or pull
    invoice_id: Optional[str] = None  # For single push
    invoice_ids: Optional[List[str]] = None  # For batch push
    since: Optional[datetime] = None  # For pull
    limit: int = 100


class ConnectorSyncResponse(BaseModel):
    success: bool
    connector_type: str
    direction: str
    records_synced: int = 0
    records_failed: int = 0
    erp_ids: List[str] = []
    errors: List[str] = []
    warnings: List[str] = []
    raw_response: Optional[Dict[str, Any]] = None


class ConnectorTestResponse(BaseModel):
    connector: str
    status: str
    sandbox: bool
    message: str
    auth_url: Optional[str] = None
    tenant_id: Optional[str] = None


class AvailableConnector(BaseModel):
    type: str
    display_name: str
    directions: List[str]
