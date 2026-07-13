"""SAP ERP connector — RFC/REST integration.

Skeleton implementation with sandbox support.
Production requires SAP RFC SDK (pyrfc) or SAP Business One DI API.
"""

import logging
from datetime import datetime
from typing import Any, Dict, List, Optional

import httpx

from app.connectors.base import BaseERPConnector, ConnectorType, ERPInvoice, SyncDirection, SyncResult

logger = logging.getLogger(__name__)


class SAPConnector(BaseERPConnector):
    """SAP ERP connector supporting RFC and REST APIs."""

    connector_type = ConnectorType.SAP
    display_name = "SAP"
    supported_directions = [SyncDirection.PUSH, SyncDirection.PULL]

    def __init__(self, **kwargs):
        super().__init__(**kwargs)
        # SAP-specific config
        self.sap_client = self.extra_config.get("sap_client", "100")
        self.sap_system_id = self.extra_config.get("sap_system_id", "")
        self.use_rest = self.extra_config.get("use_rest", "true").lower() == "true"
        # SAP REST API base URLs
        if self.sandbox:
            self.api_base = self.base_url or "https://sandbox.sap.com/api"
        else:
            self.api_base = self.base_url or "https://api.sap.com/api"

    async def authenticate(self) -> bool:
        """Authenticate with SAP using OAuth2 or Basic Auth."""
        if self.sandbox:
            logger.info("SAP Sandbox: simulating authentication")
            return True

        try:
            async with httpx.AsyncClient() as client:
                if self.use_rest:
                    # SAP BTP / S/4HANA Cloud REST auth
                    resp = await client.post(
                        f"{self.api_base}/oauth/token",
                        data={
                            "grant_type": "client_credentials",
                            "client_id": self.api_key,
                            "client_secret": self.api_secret,
                        },
                        timeout=30,
                    )
                    resp.raise_for_status()
                    self._token = resp.json().get("access_token", "")
                    return True
                else:
                    # Basic auth for on-premise SAP
                    return True
        except Exception as e:
            logger.error(f"SAP authentication failed: {e}")
            return False

    async def push_invoice(self, invoice: ERPInvoice) -> SyncResult:
        """Push invoice to SAP as a FI document or BAPI."""
        if self.sandbox:
            return self._sandbox_push(invoice)

        try:
            async with httpx.AsyncClient() as client:
                headers = {"Authorization": f"Bearer {self._token}"}
                payload = self._build_sap_payload(invoice)

                resp = await client.post(
                    f"{self.api_base}/sap/opu/odata/sap/API_INVOIC_PROCESSING/InvoiceSet",
                    json=payload,
                    headers=headers,
                    timeout=60,
                )
                resp.raise_for_status()
                data = resp.json()
                erp_id = data.get("d", {}).get("InvoiceId", "")

                return SyncResult(
                    success=True,
                    connector_type="sap",
                    direction="push",
                    records_synced=1,
                    erp_ids=[erp_id],
                    raw_response=data,
                )
        except Exception as e:
            logger.error(f"SAP push failed: {e}")
            return SyncResult(
                success=False,
                connector_type="sap",
                direction="push",
                records_failed=1,
                errors=[str(e)],
            )

    async def pull_invoices(
        self, since: Optional[datetime] = None, limit: int = 100
    ) -> SyncResult:
        """Pull invoices from SAP."""
        if self.sandbox:
            return SyncResult(
                success=True,
                connector_type="sap",
                direction="pull",
                records_synced=0,
                warnings=["Sandbox mode: no invoices to pull"],
            )

        try:
            async with httpx.AsyncClient() as client:
                headers = {"Authorization": f"Bearer {self._token}"}
                params = {"$top": limit, "$format": "json"}
                if since:
                    params["$filter"] = f"CreatedDate ge datetime'{since.isoformat()}'"

                resp = await client.get(
                    f"{self.api_base}/sap/opu/odata/sap/API_INVOIC_PROCESSING/InvoiceSet",
                    params=params,
                    headers=headers,
                    timeout=60,
                )
                resp.raise_for_status()
                data = resp.json()
                invoices = data.get("d", {}).get("results", [])

                return SyncResult(
                    success=True,
                    connector_type="sap",
                    direction="pull",
                    records_synced=len(invoices),
                    raw_response={"count": len(invoices)},
                )
        except Exception as e:
            logger.error(f"SAP pull failed: {e}")
            return SyncResult(
                success=False,
                connector_type="sap",
                direction="pull",
                errors=[str(e)],
            )

    async def check_status(self, erp_id: str) -> Dict[str, Any]:
        """Check invoice status in SAP."""
        if self.sandbox:
            return {"erp_id": erp_id, "status": "posted", "sandbox": True}

        try:
            async with httpx.AsyncClient() as client:
                headers = {"Authorization": f"Bearer {self._token}"}
                resp = await client.get(
                    f"{self.api_base}/sap/opu/odata/sap/API_INVOIC_PROCESSING/InvoiceSet('{erp_id}')",
                    headers=headers,
                    timeout=30,
                )
                resp.raise_for_status()
                return resp.json().get("d", {})
        except Exception as e:
            return {"error": str(e)}

    async def test_connection(self) -> Dict[str, Any]:
        """Test SAP connectivity."""
        if self.sandbox:
            return {
                "connector": "sap",
                "status": "ok",
                "sandbox": True,
                "message": "Sandbox mode — connection simulated",
            }

        auth_ok = await self.authenticate()
        return {
            "connector": "sap",
            "status": "ok" if auth_ok else "error",
            "sandbox": False,
            "message": "Authentication successful" if auth_ok else "Authentication failed",
        }

    def _sandbox_push(self, invoice: ERPInvoice) -> SyncResult:
        """Simulate pushing to SAP in sandbox mode."""
        mock_erp_id = f"SAP-{invoice.invoice_number[:20]}"
        logger.info(f"SAP Sandbox: simulated push for invoice {invoice.invoice_number}")
        return SyncResult(
            success=True,
            connector_type="sap",
            direction="push",
            records_synced=1,
            erp_ids=[mock_erp_id],
            raw_response={
                "sandbox": True,
                "message": f"Invoice {invoice.invoice_number} would be posted as SAP document {mock_erp_id}",
                "DocumentNumber": mock_erp_id,
                "FiscalYear": str(datetime.utcnow().year),
                "CompanyCode": self.sap_client,
            },
        )

    def _build_sap_payload(self, invoice: ERPInvoice) -> Dict[str, Any]:
        """Build SAP OData payload from ERPInvoice."""
        return {
            "InvoiceNumber": invoice.invoice_number,
            "VendorId": invoice.vendor_tax_id or "",
            "VendorName": invoice.vendor_name,
            "CustomerName": invoice.buyer_name or "",
            "InvoiceDate": (
                invoice.invoice_date.isoformat() if invoice.invoice_date
                else datetime.utcnow().isoformat()
            ),
            "DueDate": (
                invoice.due_date.isoformat() if invoice.due_date
                else ""
            ),
            "Currency": invoice.currency,
            "GrossAmount": str(invoice.total_amount),
            "TaxAmount": str(invoice.tax_amount),
            "CompanyCode": self.sap_client,
            "Items": [
                {
                    "ItemNumber": str(i + 1),
                    "Description": line.get("description", ""),
                    "Quantity": str(line.get("quantity", 1)),
                    "UnitPrice": str(line.get("unit_price", 0)),
                    "Amount": str(line.get("line_total", 0)),
                    "TaxRate": str(line.get("tax_rate", 0)),
                }
                for i, line in enumerate(invoice.lines)
            ],
        }
