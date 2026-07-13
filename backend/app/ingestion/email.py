"""Email (IMAP) ingestion channel — polls mailbox for invoice attachments."""

import email
import imaplib
import logging
from email.header import decode_header
from typing import Optional

from app.config import settings
from app.ingestion.base import BaseIngestion, IngestedDocument

logger = logging.getLogger(__name__)


class EmailIngestion(BaseIngestion):
    """IMAP-based email ingestion.

    Connects to an IMAP server, searches for unseen emails (optionally filtered
    by subject/sender), and extracts invoice attachments (PDF, XML, images).
    """

    def __init__(self, tenant_id: str):
        super().__init__(tenant_id, source_type="email")
        self.host = settings.IMAP_HOST
        self.port = settings.IMAP_PORT
        self.username = settings.IMAP_USERNAME
        self.password = settings.IMAP_PASSWORD
        self.folder = settings.IMAP_FOLDER

    def _connect(self) -> imaplib.IMAP4_SSL:
        """Establish IMAP connection."""
        if not self.host:
            raise ValueError("IMAP_HOST is not configured")
        mail = imaplib.IMAP4_SSL(self.host, self.port)
        mail.login(self.username, self.password)
        return mail

    @staticmethod
    def _decode_header_value(header_value: Optional[str]) -> str:
        """Decode an email header value that may be encoded."""
        if not header_value:
            return ""
        decoded_parts = decode_header(header_value)
        result = []
        for part, charset in decoded_parts:
            if isinstance(part, bytes):
                result.append(part.decode(charset or "utf-8", errors="replace"))
            else:
                result.append(part)
        return " ".join(result)

    @staticmethod
    def _get_attachment_filename(part) -> Optional[str]:
        """Extract filename from a MIME part."""
        filename = part.get_filename()
        if filename:
            return EmailIngestion._decode_header_value(filename)
        return None

    def _extract_attachments(self, msg: email.message.Message) -> list[IngestedDocument]:
        """Extract all supported attachments from an email message."""
        documents = []
        message_id = msg.get("Message-ID", "")
        subject = self._decode_header_value(msg.get("Subject", ""))
        sender = msg.get("From", "")

        for part in msg.walk():
            content_disposition = str(part.get("Content-Disposition", ""))

            # Only process attachments (not inline images, etc.)
            if "attachment" not in content_disposition and not part.get_filename():
                continue

            filename = self._get_attachment_filename(part)
            if not filename:
                continue

            if not self.is_supported(filename, part.get_content_type()):
                self.logger.info(f"Skipping unsupported attachment: {filename}")
                continue

            content = part.get_payload(decode=True)
            if not content:
                continue

            documents.append(
                IngestedDocument(
                    filename=filename,
                    content=content,
                    mime_type=part.get_content_type() or "application/octet-stream",
                    source_reference=message_id,
                    metadata={
                        "subject": subject,
                        "sender": sender,
                        "message_id": message_id,
                    },
                )
            )

        return documents

    async def poll(self) -> list[IngestedDocument]:
        """Poll IMAP mailbox for new emails with invoice attachments.

        Searches for UNSEEN emails, extracts supported attachments,
        and marks processed emails as SEEN.
        """
        all_documents = []
        mail = None

        try:
            mail = self._connect()
            mail.select(self.folder)

            # Search for unseen emails
            status, message_ids = mail.search(None, "UNSEEN")
            if status != "OK" or not message_ids[0]:
                self.logger.info("No new emails found")
                return []

            ids = message_ids[0].split()
            self.logger.info(f"Found {len(ids)} unseen emails")

            for msg_id in ids:
                try:
                    status, data = mail.fetch(msg_id, "(RFC822)")
                    if status != "OK":
                        continue

                    raw_email = data[0][1]
                    msg = email.message_from_bytes(raw_email)

                    attachments = self._extract_attachments(msg)
                    if attachments:
                        all_documents.extend(attachments)
                        self.logger.info(
                            f"Extracted {len(attachments)} attachments from "
                            f"message {msg_id.decode()}"
                        )

                    # Mark as seen
                    mail.store(msg_id, "+FLAGS", "\\Seen")

                except Exception as e:
                    self.logger.error(f"Error processing email {msg_id}: {e}")
                    continue

        except imaplib.IMAP4.error as e:
            self.logger.error(f"IMAP connection error: {e}")
            raise
        except Exception as e:
            self.logger.error(f"Email polling error: {e}")
            raise
        finally:
            if mail:
                try:
                    mail.logout()
                except Exception:
                    pass

        self.logger.info(f"Total documents extracted: {len(all_documents)}")
        return all_documents

    async def fetch(self, reference: str) -> IngestedDocument:
        """Fetch a specific email by Message-ID and extract its attachments.

        Args:
            reference: The Message-ID header value.
        """
        mail = self._connect()
        try:
            mail.select(self.folder)

            # Search by Message-ID
            status, data = mail.search(None, f'(HEADER Message-ID "{reference}")')
            if status != "OK" or not data[0]:
                raise ValueError(f"Email not found: {reference}")

            msg_id = data[0].split()[0]
            status, fetch_data = mail.fetch(msg_id, "(RFC822)")
            if status != "OK":
                raise ValueError(f"Failed to fetch email: {reference}")

            raw_email = fetch_data[0][1]
            msg = email.message_from_bytes(raw_email)
            attachments = self._extract_attachments(msg)

            if not attachments:
                raise ValueError(f"No supported attachments in email: {reference}")

            return attachments[0]  # Return first attachment

        finally:
            mail.logout()
