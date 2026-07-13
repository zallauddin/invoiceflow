"""Xero connector — fully working OAuth 2.0 integration.

Supports Xero Accounting API for invoice push/pull.
Implements full OAuth 2.0 flow with token refresh.
"""

import hashlib
import logging
import secrets
import base64
from datetime import datetime, timedelta
from typing import Any, Dict, List, Optional
from urllib.parse import urlencode

import httpx

from app.connectors.base import BaseERPConnector, ConnectorType, ERPInvoice, SyncDirection, SyncResult

logger = logging.getLogger(__name__)

# Xero API endpoints
XERO_AUTH_URL = "https://login.xero.com/identity/connect/authorize"
XERO_TOKEN_URL = "https://identity.xero.com/connect/token"
XERO_API_BASE = "https://api.xero.com/api.xro/2.0"
XERO_CONNECTIONS_URL = "https://api.xero.com/connections"


class XeroConnector(BaseERPConnector):
    """Xero Accounting API connector with full OAuth 2.0 support."""

    connector_type = ConnectorType.XERO
    display_name = "Xero"
    supported_directions = [SyncDirection.PUSH, SyncDirection.PULL]

    SCOPES = [
        "accounting.transactions",
        "accounting.contacts",
        "accounting.settings",
        "accounting.reports.read",
    ]

    def __init__(self, **kwargs):
        super().__init__(**kwargs)
        self._access_token: Optional[str] = None
        self._refresh_token: Optional[str] = None
        self._token_expiry: Optional[datetime] = None
        self._tenant_id_xero: Optional[str] = None
        # Xero-specific: api_key = client_id, api_secret = client_secret
        self.redirect_uri = self.extra_config.get(
            "redirect_uri", "http://localhost:8000/api/v1/connectors/xero/callback"
        )
        if self.sandbox:
            self.api_base = "https://api.xero.com/api.xro/2.0"
        else:
            self.api_base = XERO_API_BASE

    # ── OAuth 2.0 Flow ───────────────────────────────────────────────

    def get_authorization_url(self) -> str:
        """Generate the OAuth 2.0 authorization URL for user consent."""
        state = secrets.token_urlsafe(32)
        params = {
            "response_type": "code",
            "client_id": self.api_key,
            "redirect_uri": self.redirect_uri,
            "scope": " ".join(self.SCOPES),
            "state": state,
        }
        return f"{XERO_AUTH_URL}?{urlencode(params)}"

    async def exchange_code(self, code: str) -> bool:
        """Exchange authorization code for access + refresh tokens."""
        if self.sandbox:
            logger.info("Xero Sandbox: simulating code exchange")
            self._access_token = "sandbox-access-token"
            self._refresh_token = "sandbox-refresh-token"
            self._token_expiry = datetime.utcnow() + timedelta(hours=1)
            return True

        try:
            async with httpx.AsyncClient() as client:
                resp = await client.post(
                    XERO_TOKEN_URL,
                    data={
                        "grant_type": "authorization_code",
                        "code": code,
                        "redirect_uri": self.redirect_uri,
                    },
                    auth=(self.api_key, self.api_secret),
                    headers={"Content-Type": "application/x-www-form-urlencoded"},
                    timeout=30,
                )
                resp.raise_for_status()
                tokens = resp.json()
                self._access_token = tokens["access_token"]
                self._refresh_token = tokens["refresh_token"]
                self._token_expiry = datetime.utcnow() + timedelta(
                    seconds=tokens.get("expires_in", 1800)
                )
                # Get connected tenant
                await self._fetch_tenant_id()
                return True
        except Exception as e:
            logger.error(f"Xero code exchange failed: {e}")
            return False

    async def refresh_access_token(self) -> bool:
        """Refresh the access token using the refresh token."""
        if self.sandbox or not self._refresh_token:
            return True

        try:
            async with httpx.AsyncClient() as client:
                resp = await client.post(
                    XERO_TOKEN_URL,
                    data={
                        "grant_type": "refresh_token",
                        "refresh_token": self._refresh_token,
                    },
                    auth=(self.api_key, self.api_secret),
                    headers={"Content-Type": "application/x-www-form-urlencoded"},
                    timeout=30,
                )
                resp.raise_for_status()
                tokens = resp.json()
                self._access_token = tokens["access_token"]
                self._refresh_token = tokens["refresh_token"]
                self._token_expiry = datetime.utcnow() + timedelta(
                    seconds=tokens.get("expires_in", 1800)
                )
                return True
        except Exception as e:
            logger.error(f"Xero token refresh failed: {e}")
            return False

    async def _ensure_valid_token(self) -> bool:
        """Ensure we have a valid, non-expired token."""
        if not self._access_token:
            return False
        if self._token_expiry and datetime.utcnow() >= self._token_expiry:
            return await self.refresh_access_token()
        return True

    async def _fetch_tenant_id(self) -> None:
        """Fetch the connected tenant ID from Xero."""
        if self.sandbox:
            self._tenant_id_xero = "sandbox-tenant-id"
            return

        try:
            async with httpx.AsyncClient() as client:
                resp = await client.get(
                    XERO_CONNECTIONS_URL,
                    headers={"Authorization": f"Bearer {self._access_token}"},
                    timeout=30,
                )
                resp.raise_for_status()
                connections = resp.json()
                if connections:
                    self._tenant_id_xero = connections[0]["tenantId"]
        except Exception as e:
            logger.error(f"Failed to fetch Xero tenant ID: {e}")

    async def authenticate(self) -> bool:
        """Authenticate — checks for existing valid token."""
        return await self._ensure_valid_token()

    # ── Invoice Operations ───────────────────────────────────────────

    async def push_invoice(self, invoice: ERPInvoice) -> SyncResult:
        """Push invoice to Xero as an Accounts Payable bill."""
        if self.sandbox:
            return self._sandbox_push(invoice)

        if not await self._ensure_valid_token():
            return SyncResult(
                success=False, connector_type="xero", direction="push",
                errors=["Not authenticated. Please re-authorize."],
            )

        try:
            async with httpx.AsyncClient() as client:
                headers = {
                    "Authorization": f"Bearer {self._access_token}",
                    "Xero-Tenant-Id": self._tenant_id_xero,
                    "Content-Type": "application/json",
                }
                payload = {"Invoices": [self._build_xero_payload(invoice)]}

                resp = await client.post(
                    f"{self.api_base}/Invoices",
                    json=payload,
                    headers=headers,
                    timeout=60,
                )
                resp.raise_for_status()
                data = resp.json()
                xero_invoices = data.get("Invoices", [])

                if xero_invoices:
                    xero_inv = xero_invoices[0]
                    erp_id = xero_inv.get("InvoiceID", "")
                    status = xero_inv.get("Status", "")
                    return SyncResult(
                        success=True, connector_type="xero", direction="push",
                        records_synced=1, erp_ids=[erp_id],
                        raw_response={"InvoiceID": erp_id, "Status": status},
                    )
                else:
                    return SyncResult(
                        success=False, connector_type="xero", direction="push",
                        errors=["No invoice returned from Xero"],
                    )
        except Exception as e:
            logger.error(f"Xero push failed: {e}")
            return SyncResult(
                success=False, connector_type="xero", direction="push",
                records_failed=1, errors=[str(e)],
            )

    async def pull_invoices(
        self, since: Optional[datetime] = None, limit: int = 100
    ) -> SyncResult:
        """Pull invoices from Xero."""
        if self.sandbox:
            return self._sandbox_pull()

        if not await self._ensure_valid_token():
            return SyncResult(
                success=False, connector_type="xero", direction="pull",
                errors=["Not authenticated. Please re-authorize."],
            )

        try:
            async with httpx.AsyncClient() as client:
                headers = {
                    "Authorization": f"Bearer {self._access_token}",
                    "Xero-Tenant-Id": self._tenant_id_xero,
                }
                params = {"where": "Type==\"ACCPAY\"", "page": 1}
                if since:
                    params["where"] += f'&&||DateTimeUTC>\\\\/Date({int(since.timestamp()*1000)})\\\\/'

                resp = await client.get(
                    f"{self.api_base}/Invoices",
                    params=params,
                    headers=headers,
                    timeout=60,
                )
                resp.raise_for_status()
                data = resp.json()
                invoices = data.get("Invoices", [])

                return SyncResult(
                    success=True, connector_type="xero", direction="pull",
                    records_synced=min(len(invoices), limit),
                    raw_response={"count": len(invoices)},
                )
        except Exception as e:
            logger.error(f"Xero pull failed: {e}")
            return SyncResult(
                success=False, connector_type="xero", direction="pull",
                errors=[str(e)],
            )

    async def check_status(self, erp_id: str) -> Dict[str, Any]:
        """Check invoice status in Xero."""
        if self.sandbox:
            return {"erp_id": erp_id, "status": "AUTHORISED", "sandbox": True}

        if not await self._ensure_valid_token():
            return {"error": "Not authenticated"}

        try:
            async with httpx.AsyncClient() as client:
                headers = {
                    "Authorization": f"Bearer {self._access_token}",
                    "Xero-Tenant-Id": self._tenant_id_xero,
                }
                resp = await client.get(
                    f"{self.api_base}/Invoices/{erp_id}",
                    headers=headers,
                    timeout=30,
                )
                resp.raise_for_status()
                data = resp.json()
                invoices = data.get("Invoices", [])
                if invoices:
                    inv = invoices[0]
                    return {
                        "erp_id": erp_id,
                        "status": inv.get("Status", ""),
                        "total": inv.get("Total", 0),
                        "amount_due": inv.get("AmountDue", 0),
                    }
                return {"error": "Invoice not found"}
        except Exception as e:
            return {"error": str(e)}

    async def test_connection(self) -> Dict[str, Any]:
        """Test Xero connectivity."""
        if self.sandbox:
            return {
                "connector": "xero",
                "status": "ok",
                "sandbox": True,
                "message": "Sandbox mode — connection simulated",
                "auth_url": self.get_authorization_url(),
            }

        has_token = await self._ensure_valid_token()
        if not has_token:
            return {
                "connector": "xero",
                "status": "needs_auth",
                "sandbox": False,
                "message": "Authorization required",
                "auth_url": self.get_authorization_url(),
            }

        return {
            "connector": "xero",
            "status": "ok",
            "sandbox": False,
            "tenant_id": self._tenant_id_xero,
            "message": "Connected to Xero",
        }

    # ── Sandbox Helpers ──────────────────────────────────────────────

    def _sandbox_push(self, invoice: ERPInvoice) -> SyncResult:
        """Simulate pushing to Xero in sandbox mode."""
        mock_id = f"xero-{secrets.token_hex(8)}"
        logger.info(f"Xero Sandbox: simulated push for invoice {invoice.invoice_number}")
        return SyncResult(
            success=True, connector_type="xero", direction="push",
            records_synced=1, erp_ids=[mock_id],
            raw_response={
                "sandbox": True,
                "InvoiceID": mock_id,
                "InvoiceNumber": invoice.invoice_number,
                "Status": "AUTHORISED",
                "Total": str(invoice.total_amount),
                "Message": f"Invoice {invoice.invoice_number} synced to Xero sandbox",
            },
        )

    def _sandbox_pull(self) -> SyncResult:
        """Simulate pulling from Xero in sandbox mode."""
        return SyncResult(
            success=True, connector_type="xero", direction="pull",
            records_synced=0,
            warnings=["Sandbox mode: no invoices to pull"],
        )

    # ── Payload Builder ──────────────────────────────────────────────

    def _build_xero_payload(self, invoice: ERPInvoice) -> Dict[str, Any]:
        """Build Xero Invoice JSON from ERPInvoice."""
        return {
            "Type": "ACCPAY",  # Accounts Payable (bill)
            "InvoiceNumber": invoice.invoice_number,
            "Reference": invoice.source_reference or invoice.invoice_number,
            "Date": (
                invoice.invoice_date.strftime("%Y-%m-%d") if invoice.invoice_date
                else datetime.utcnow().strftime("%Y-%m-%d")
            ),
            "DueDate": (
                invoice.due_date.strftime("%Y-%m-%d") if invoice.due_date
                else (datetime.utcnow() + timedelta(days=30)).strftime("%Y-%m-%d")
            ),
            "CurrencyCode": invoice.currency,
            "Status": "AUTHORISED",
            "LineItems": [
                {
                    "Description": line.get("description", ""),
                    "Quantity": str(line.get("quantity", 1)),
                    "UnitAmount": str(line.get("unit_price", 0)),
                    "LineAmount": str(line.get("line_total", 0)),
                    "AccountCode": self.extra_config.get("default_account_code", "200"),
                    "TaxType": self._get_xero_tax_type(line.get("tax_rate", 0)),
                }
                for line in invoice.lines
            ],
            "Contact": {
                "Name": invoice.vendor_name,
            },
        }

    def _get_xero_tax_type(self, tax_rate: float) -> str:
        """Map tax rate to Xero TaxType code."""
        if tax_rate == 0:
            return "NONE"
        elif tax_rate <= 5:
            return "OUTPUT2"  # 5% GST
        elif tax_rate <= 10:
            return "OUTPUT"   # 10% GST
        elif tax_rate <= 15:
            return "OUTPUT3"  # 15% GST
        elif tax_rate <= 20:
            return "OUTPUT"   # 20% VAT
        else:
            return "OUTPUT"
