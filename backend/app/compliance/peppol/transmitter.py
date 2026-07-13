"""PEPPOL AS4 submission (sandbox mode).

Handles transmission of validated UBL 2.1 invoices to the PEPPOL network.
Supports sandbox mode for testing and production mode for live submission.

In production, this would use the AS4 messaging protocol via a PEPPOL Access Point.
For sandbox mode, we simulate the submission and return a mock response.
"""

import hashlib
import logging
import uuid
from datetime import datetime
from dataclasses import dataclass, field
from typing import Any

import httpx

from app.config import settings

logger = logging.getLogger(__name__)


@dataclass
class TransmissionResult:
    """Result of a PEPPOL transmission."""
    success: bool = False
    message_id: str = ""
    submission_id: str = ""
    timestamp: str = ""
    status: str = "pending"
    raw_response: dict = field(default_factory=dict)
    error: str = ""


class PEPPOLTransmitter:
    """
    Transmits validated UBL 2.1 invoices to the PEPPOL network.

    Supports:
    - Sandbox mode: Simulates transmission for testing
    - Production mode: Real AS4 transmission via Access Point
    """

    # Sandbox endpoints (PEPPOL test AP)
    SANDBOX_BASE_URL = "https://ap-test.peppol.eu"
    SANDBOX_SUBMIT_PATH = "/as4"

    # Production Peppol Directory API
    DIRECTORY_URL = "https://directory.peppol.eu"

    def __init__(self, sandbox: bool = True):
        self.sandbox = sandbox
        self._client: httpx.AsyncClient | None = None

    async def _get_client(self) -> httpx.AsyncClient:
        """Get or create HTTP client."""
        if self._client is None or self._client.is_closed:
            self._client = httpx.AsyncClient(timeout=30.0)
        return self._client

    async def close(self) -> None:
        """Close HTTP client."""
        if self._client and not self._client.is_closed:
            await self._client.aclose()

    async def transmit(
        self,
        ubl_xml: str,
        invoice_data: dict[str, Any],
        recipient_endpoint_id: str,
        sender_endpoint_id: str,
    ) -> TransmissionResult:
        """
        Transmit a PEPPOL invoice.

        Args:
            ubl_xml: The validated UBL 2.1 XML string
            invoice_data: Invoice metadata
            recipient_endpoint_id: Recipient's PEPPOL endpoint ID
            sender_endpoint_id: Sender's PEPPOL endpoint ID

        Returns:
            TransmissionResult with status and details
        """
        message_id = str(uuid.uuid4())
        timestamp = datetime.utcnow().isoformat() + "Z"

        result = TransmissionResult(
            message_id=message_id,
            timestamp=timestamp,
            status="submitted",
        )

        if self.sandbox:
            result = await self._sandbox_transmit(ubl_xml, invoice_data, message_id, timestamp)
        else:
            result = await self._production_transmit(
                ubl_xml, invoice_data, recipient_endpoint_id, sender_endpoint_id, message_id, timestamp
            )

        return result

    async def _sandbox_transmit(
        self,
        ubl_xml: str,
        invoice_data: dict[str, Any],
        message_id: str,
        timestamp: str,
    ) -> TransmissionResult:
        """
        Simulate PEPPOL transmission in sandbox mode.
        Returns a successful response with mock submission ID.
        """
        logger.info(f"[SANDBOX] Simulating PEPPOL transmission for invoice {invoice_data.get('invoice_number')}")

        # Simulate network delay
        import asyncio
        await asyncio.sleep(0.1)

        submission_id = f"PEPPOL-{uuid.uuid4().hex[:12].upper()}"
        invoice_number = invoice_data.get("invoice_number", "UNKNOWN")

        # Build mock response
        result = TransmissionResult(
            success=True,
            message_id=message_id,
            submission_id=submission_id,
            timestamp=timestamp,
            status="accepted",
            raw_response={
                "response_code": "APPROVED",
                "submission_id": submission_id,
                "message_id": message_id,
                "invoice_number": invoice_number,
                "ap_endpoint": "https://ap-test.peppol.eu",
                "received_at": timestamp,
                "processing_status": "SUCCESS",
                "note": "This is a sandbox test submission — no real network transmission occurred.",
            },
        )

        logger.info(f"[SANDBOX] Transmission successful: {submission_id}")
        return result

    async def _production_transmit(
        self,
        ubl_xml: str,
        invoice_data: dict[str, Any],
        recipient_endpoint_id: str,
        sender_endpoint_id: str,
        message_id: str,
        timestamp: str,
    ) -> TransmissionResult:
        """
        Production AS4 transmission via PEPPOL Access Point.

        In a real implementation, this would:
        1. Sign the UBL XML with the sender's certificate
        2. Wrap in AS4/SOAP envelope
        3. POST to the Access Point endpoint
        4. Parse the AS4 response for acceptance/rejection
        """
        logger.info(f"[PRODUCTION] Attempting PEPPOL transmission for invoice {invoice_data.get('invoice_number')}")

        # Check Peppol Directory for recipient routing info
        routing_info = await self._lookup_recipient(recipient_endpoint_id)
        if not routing_info:
            return TransmissionResult(
                success=False,
                message_id=message_id,
                timestamp=timestamp,
                status="rejected",
                error=f"Recipient endpoint {recipient_endpoint_id} not found in Peppol Directory",
            )

        ap_url = routing_info.get("ap_url", "")

        # Build AS4/SOAP envelope (simplified)
        soap_envelope = self._build_as4_envelope(
            ubl_xml=ubl_xml,
            message_id=message_id,
            timestamp=timestamp,
            sender_id=sender_endpoint_id,
            recipient_id=recipient_endpoint_id,
        )

        # Transmit to Access Point
        client = await self._get_client()
        try:
            response = await client.post(
                ap_url,
                content=soap_envelope,
                headers={
                    "Content-Type": "application/soap+xml; charset=utf-8",
                },
                verify=True,
            )

            if response.status_code == 200:
                return TransmissionResult(
                    success=True,
                    message_id=message_id,
                    submission_id=f"PEPPOL-{uuid.uuid4().hex[:12].upper()}",
                    timestamp=timestamp,
                    status="accepted",
                    raw_response={"http_status": 200, "body": response.text[:500]},
                )
            else:
                return TransmissionResult(
                    success=False,
                    message_id=message_id,
                    timestamp=timestamp,
                    status="rejected",
                    error=f"Access Point returned HTTP {response.status_code}: {response.text[:300]}",
                    raw_response={"http_status": response.status_code, "body": response.text[:500]},
                )

        except httpx.RequestError as e:
            logger.error(f"[PRODUCTION] Transmission failed: {e}")
            return TransmissionResult(
                success=False,
                message_id=message_id,
                timestamp=timestamp,
                status="error",
                error=f"Network error: {str(e)}",
            )

    async def _lookup_recipient(self, endpoint_id: str) -> dict[str, Any] | None:
        """
        Look up recipient Access Point URL in Peppol Directory.

        In production, queries the Peppol Directory API to find which
        Access Point the recipient is registered with.
        """
        if self.sandbox:
            # Return mock AP URL for sandbox
            return {
                "ap_url": f"{self.SANDBOX_BASE_URL}{self.SANDBOX_SUBMIT_PATH}",
                "participant_id": endpoint_id,
                "business_card": {"name": "Sandbox Test Recipient"},
            }

        client = await self._get_client()
        try:
            response = await client.get(
                f"{self.DIRECTORY_URL}/iso6523-actorid-upis::{endpoint_id}",
                headers={"Accept": "application/json"},
            )
            if response.status_code == 200:
                data = response.json()
                services = data.get("services", [])
                for service in services:
                    if service.get("identifier", {}).get("type") == "ap":
                        return {
                            "ap_url": service.get("endpoint", {}).get("url", ""),
                            "participant_id": endpoint_id,
                            "business_card": data.get("businessCard", {}),
                        }
            return None
        except Exception as e:
            logger.error(f"Directory lookup failed: {e}")
            return None

    @staticmethod
    def _build_as4_envelope(
        ubl_xml: str,
        message_id: str,
        timestamp: str,
        sender_id: str,
        recipient_id: str,
    ) -> str:
        """Build a simplified AS4/SOAP envelope for PEPPOL transmission."""
        # Note: Production would use proper WS-Security signing and EbMS3 headers
        envelope = f"""<?xml version="1.0" encoding="UTF-8"?>
<soap:Envelope xmlns:soap="http://www.w3.org/2003/05/soap-envelope"
               xmlns:eb3="http://docs.oasis-open.org/ebxml-msg/ebms/3.0/core/xsd/200704"
               xmlns:wsse="http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd">
  <soap:Header>
    <eb3:Messaging>
      <eb3:UserMessage>
        <eb3:MessageInfo>
          <eb3:MessageId>{message_id}</eb3:MessageId>
          <eb3:Timestamp>{timestamp}</eb3:Timestamp>
        </eb3:MessageInfo>
        <eb3:PartyInfo>
          <eb3:From><eb3:PartyId type="iso6523-actorid-upis">{sender_id}</eb3:PartyId></eb3:From>
          <eb3:To><eb3:PartyId type="iso6523-actorid-upis">{recipient_id}</eb3:PartyId></eb3:To>
        </eb3:PartyInfo>
        <eb3:PayloadInfo>
          <eb3:Info>
            <eb3:Service>busdox-docbus-w3c-ubl</eb3:Service>
            <eb3:Action>urn:fdc:peppol.eu:2017:poacc:billing:01:1.0#Invoice-02</eb3:Action>
          </eb3:Info>
        </eb3:PayloadInfo>
      </eb3:UserMessage>
    </eb3:Messaging>
  </soap:Header>
  <soap:Body>
    <eb3:Payload>
      <eb3:Body>{ubl_xml}</eb3:Body>
    </eb3:Payload>
  </soap:Body>
</soap:Envelope>"""
        return envelope
