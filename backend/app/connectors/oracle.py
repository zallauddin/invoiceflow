"""Oracle ERP Cloud connector — REST API integration.

Skeleton implementation with sandbox support.
Production requires Oracle Cloud REST API access (Fusion Applications).
"""

import logging
from datetime import datetime
from typing import Any, Dict, List, Optional

import httpx

from app.connectors.base import BaseERPConnector, ConnectorType, ERPInvoice, SyncDirection, SyncResult

logger = logging.getLogger(__name__)


class OracleConnector(BaseERPConnector):
    """Oracle ERP Cloud connector using REST API."""

    connector_type = ConnectorType.ORACLE
    display_name = "Oracle ERP Cloud"
    supported_directions = [SyncDirection.PUSH, SyncDirection.PULL]

    def __init__(self, **kwargs):
        super().__init__(**kwargs)
        self.oracle_bu = self.extra_config.get("business_unit", "US1 Business Unit")
        self.ledger = self.extra_config.get("ledger", "Primary Ledger")
        if self.sandbox:
            self.api_base = self.base_url or "https://sandbox.oraclecloud.com/fscmRestApi"
        else:
            self.api_base = self.base_url or "https://oraclecloud.com/fscmRestApi"

    async def authenticate(self) -> bool:
        """Authenticate with Oracle Cloud using OAuth2."""
        if self.sandbox:
            logger.info("Oracle Sandbox: simulating authentication")
            return True

        try:
            async with httpx.AsyncClient() as client:
                resp = await client.post(
                    f"{self.api_base}/resources/auth/api/v1/token",
                    json={
                        "grantType": "client_credentials",
                        "clientId": self.api_key,
                        "clientSecret": self.api_secret,
                    },
                    timeout=30,
                )
                resp.raise_for_status()
                self._token = resp.json().get("accessToken", "")
                return True
        except Exception as e:
            logger.error(f"Oracle authentication failed: {e}")
            return False

    async def push_invoice(self, invoice: ERPInvoice) -> SyncResult:
        """Push invoice to Oracle as an AP Invoice."""
        if self.sandbox:
            return self._sandbox_push(invoice)

        try:
            async with httpx.AsyncClient() as client:
                headers = {
                    "Authorization": f"Bearer {self._token}",
                    "Content-Type": "application/json",
                }
                payload = self._build_oracle_payload(invoice)

                resp = await client.post(
                    f"{self.api_base}/resources/latest/invoices",
                    json=payload,
                    headers=headers,
                    timeout=60,
                )
                resp.raise_for_status()
                data = resp.json()
                erp_id = data.get("InvoiceId", "")

                return SyncResult(
                    success=True,
                    connector_type="oracle",
                    direction="push",
                    records_synced=1,
                    erp_ids=[str(erp_id)],
                    raw_response=data,
                )
        except Exception as e:
            logger.error(f"Oracle push failed: {e}")
            return SyncResult(
                success=False,
                connector_type="oracle",
                direction="push",
                records_failed=1,
                errors=[str(e)],
            )

    async def pull_invoices(
        self, since: Optional[datetime] = None, limit: int = 100
    ) -> SyncResult:
        """Pull invoices from Oracle."""
        if self.sandbox:
            return SyncResult(
                success=True,
                connector_type="oracle",
                direction="pull",
                records_synced=0,
                warnings=["Sandbox mode: no invoices to pull"],
            )

        try:
            async with httpx.AsyncClient() as client:
                headers = {"Authorization": f"Bearer {self._token}"}
                params = {"limit": limit, "offset": 0}
                if since:
                    params["q"] = f"InvoiceDatege{since.strftime('%Y-%m-%d')}"

                resp = await client.get(
                    f"{self.api_base}/resources/latest/invoices",
                    params=params,
                    headers=headers,
                    timeout=60,
                )
                resp.raise_for_status()
                data = resp.json()
                items = data.get("items", [])

                return SyncResult(
                    success=True,
                    connector_type="oracle",
                    direction="pull",
                    records_synced=len(items),
                    raw_response={"count": len(items)},
                )
        except Exception as e:
            logger.error(f"Oracle pull failed: {e}")
            return SyncResult(
                success=False,
                connector_type="oracle",
                direction="pull",
                errors=[str(e)],
            )

    async def check_status(self, erp_id: str) -> Dict[str, Any]:
        """Check invoice status in Oracle."""
        if self.sandbox:
            return {"erp_id": erp_id, "status": "valid", "sandbox": True}

        try:
            async with httpx.AsyncClient() as client:
                headers = {"Authorization": f"Bearer {self._token}"}
                resp = await client.get(
                    f"{self.api_base}/resources/latest/invoices/{erp_id}",
                    headers=headers,
                    timeout=30,
                )
                resp.raise_for_status()
                return resp.json()
        except Exception as e:
            return {"error": str(e)}

    async def test_connection(self) -> Dict[str, Any]:
        """Test Oracle connectivity."""
        if self.sandbox:
            return {
                "connector": "oracle",
                "status": "ok",
                "sandbox": True,
                "message": "Sandbox mode — connection simulated",
            }

        auth_ok = await self.authenticate()
        return {
            "connector": "oracle",
            "status": "ok" if auth_ok else "error",
            "sandbox": False,
            "message": "Authentication successful" if auth_ok else "Authentication failed",
        }

    def _sandbox_push(self, invoice: ERPInvoice) -> SyncResult:
        """Simulate pushing to Oracle in sandbox mode."""
        mock_erp_id = f"ORA-{invoice.invoice_number[:20]}"
        logger.info(f"Oracle Sandbox: simulated push for invoice {invoice.invoice_number}")
        return SyncResult(
            success=True,
            connector_type="oracle",
            direction="push",
            records_synced=1,
            erp_ids=[mock_erp_id],
            raw_response={
                "sandbox": True,
                "message": f"Invoice {invoice.invoice_number} would be created as Oracle AP Invoice {mock_erp_id}",
                "InvoiceId": mock_erp_id,
                "InvoiceNumber": invoice.invoice_number,
                "BusinessUnit": self.oracle_bu,
                "InvoiceStatus": "PENDING_REVIEW",
            },
        )

    def _build_oracle_payload(self, invoice: ERPInvoice) -> Dict[str, Any]:
        """Build Oracle REST API payload from ERPInvoice."""
        return {
            "BusinessUnit": self.oracle_bu,
            "InvoiceNumber": invoice.invoice_number,
            "InvoiceCurrencyCode": invoice.currency,
            "InvoiceAmount": str(invoice.total_amount),
            "InvoiceDate": (
                invoice.invoice_date.strftime("%Y-%m-%d") if invoice.invoice_date
                else datetime.utcnow().strftime("%Y-%m-%d")
            ),
            "InvoiceType": "Standard",
            "PaymentTerms": "Net30",
            "PayGroup": "Manual",
            "SupplierName": invoice.vendor_name,
            "SupplierNumber": invoice.vendor_tax_id or "",
            "InvoiceLines": [
                {
                    "LineNumber": i + 1,
                    "LineType": "Item",
                    "Description": line.get("description", ""),
                    "Amount": str(line.get("line_total", 0)),
                    "Quantity": str(line.get("quantity", 1)),
                    "UnitPrice": str(line.get("unit_price", 0)),
                    "DistributionCombination": self.extra_config.get(
                        "default_account", "01-0000-0000-0000"
                    ),
                }
                for i, line in enumerate(invoice.lines)
            ],
        }
