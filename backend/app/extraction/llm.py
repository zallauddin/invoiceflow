"""LLM-based extraction — Claude and OpenAI fallback for low-confidence OCR."""

import base64
import json
import logging
from dataclasses import dataclass
from typing import Optional

import httpx

from app.config import settings

logger = logging.getLogger(__name__)

# Structured prompt for invoice extraction
EXTRACTION_PROMPT = """You are an expert invoice data extraction system. Extract ALL fields from this invoice image/document.

Return a JSON object with EXACTLY this structure (no markdown, no code fences):
{
  "invoice_number": "string or null",
  "invoice_date": "YYYY-MM-DD or null",
  "due_date": "YYYY-MM-DD or null",
  "vendor_name": "string or null",
  "vendor_tax_id": "string or null",
  "vendor_address": "string or null",
  "buyer_name": "string or null",
  "buyer_tax_id": "string or null",
  "buyer_address": "string or null",
  "currency": "3-letter ISO code (e.g. USD, EUR) or null",
  "subtotal": 0.00,
  "tax_amount": 0.00,
  "total_amount": 0.00,
  "line_items": [
    {
      "description": "string",
      "quantity": 0.00,
      "unit_price": 0.00,
      "tax_rate": 0.00,
      "tax_amount": 0.00,
      "line_total": 0.00,
      "item_code": "string or null"
    }
  ],
  "payment_terms": "string or null",
  "notes": "string or null"
}

Rules:
- Use null for any field you cannot find
- All monetary values must be numbers (not strings)
- Dates must be YYYY-MM-DD format
- Currency must be 3-letter ISO code
- Line items should include ALL line items found
- Extract data exactly as written, do not interpret or calculate
"""


@dataclass
class LLMResult:
    """Result from LLM extraction."""
    raw_response: str
    parsed_data: dict
    provider: str
    model: str
    success: bool = True
    error: Optional[str] = None


class LLMExtractor:
    """Extract invoice data using LLM when OCR confidence is low.

    Supports:
    - Anthropic Claude (claude-sonnet-4-20250514)
    - OpenAI GPT-4o
    """

    def __init__(self, provider: Optional[str] = None):
        self.provider = provider or settings.LLM_PROVIDER
        self.confidence_threshold = settings.LLM_CONFIDENCE_THRESHOLD

    def extract_from_image(
        self,
        image_bytes: bytes,
        mime_type: str = "image/png",
        context: Optional[str] = None,
    ) -> LLMResult:
        """Extract invoice data from an image using LLM vision."""
        b64_image = base64.b64encode(image_bytes).decode("utf-8")

        if self.provider == "claude":
            return self._extract_claude_image(b64_image, mime_type, context)
        else:
            return self._extract_openai_image(b64_image, mime_type, context)

    def extract_from_text(
        self,
        text: str,
        context: Optional[str] = None,
    ) -> LLMResult:
        """Extract invoice data from raw OCR text using LLM."""
        prompt = EXTRACTION_PROMPT
        if context:
            prompt += f"\n\nAdditional context:\n{context}"

        if self.provider == "claude":
            return self._extract_claude_text(text, prompt)
        else:
            return self._extract_openai_text(text, prompt)

    def _extract_claude_image(
        self, b64_image: str, mime_type: str, context: Optional[str] = None
    ) -> LLMResult:
        """Use Anthropic Claude vision API."""
        if not settings.ANTHROPIC_API_KEY:
            return LLMResult(
                raw_response="",
                parsed_data={},
                provider="claude",
                model="claude-sonnet-4-20250514",
                success=False,
                error="ANTHROPIC_API_KEY not configured",
            )

        prompt = EXTRACTION_PROMPT
        if context:
            prompt += f"\n\nAdditional context:\n{context}"

        payload = {
            "model": "claude-sonnet-4-20250514",
            "max_tokens": 4096,
            "messages": [
                {
                    "role": "user",
                    "content": [
                        {
                            "type": "image",
                            "source": {
                                "type": "base64",
                                "media_type": mime_type,
                                "data": b64_image,
                            },
                        },
                        {"type": "text", "text": prompt},
                    ],
                }
            ],
        }

        try:
            with httpx.Client(timeout=60.0) as client:
                resp = client.post(
                    "https://api.anthropic.com/v1/messages",
                    json=payload,
                    headers={
                        "x-api-key": settings.ANTHROPIC_API_KEY,
                        "anthropic-version": "2023-06-01",
                        "content-type": "application/json",
                    },
                )
                resp.raise_for_status()

            data = resp.json()
            raw_text = data["content"][0]["text"]
            parsed = self._parse_json_response(raw_text)

            return LLMResult(
                raw_response=raw_text,
                parsed_data=parsed,
                provider="claude",
                model="claude-sonnet-4-20250514",
                success=True,
            )

        except Exception as e:
            logger.error(f"Claude extraction failed: {e}")
            return LLMResult(
                raw_response="",
                parsed_data={},
                provider="claude",
                model="claude-sonnet-4-20250514",
                success=False,
                error=str(e),
            )

    def _extract_claude_text(self, text: str, prompt: str) -> LLMResult:
        """Use Anthropic Claude text API."""
        if not settings.ANTHROPIC_API_KEY:
            return LLMResult(
                raw_response="",
                parsed_data={},
                provider="claude",
                model="claude-sonnet-4-20250514",
                success=False,
                error="ANTHROPIC_API_KEY not configured",
            )

        payload = {
            "model": "claude-sonnet-4-20250514",
            "max_tokens": 4096,
            "messages": [
                {
                    "role": "user",
                    "content": f"{prompt}\n\nInvoice text:\n{text}",
                }
            ],
        }

        try:
            with httpx.Client(timeout=60.0) as client:
                resp = client.post(
                    "https://api.anthropic.com/v1/messages",
                    json=payload,
                    headers={
                        "x-api-key": settings.ANTHROPIC_API_KEY,
                        "anthropic-version": "2023-06-01",
                        "content-type": "application/json",
                    },
                )
                resp.raise_for_status()

            data = resp.json()
            raw_text = data["content"][0]["text"]
            parsed = self._parse_json_response(raw_text)

            return LLMResult(
                raw_response=raw_text,
                parsed_data=parsed,
                provider="claude",
                model="claude-sonnet-4-20250514",
                success=True,
            )

        except Exception as e:
            logger.error(f"Claude text extraction failed: {e}")
            return LLMResult(
                raw_response="",
                parsed_data={},
                provider="claude",
                model="claude-sonnet-4-20250514",
                success=False,
                error=str(e),
            )

    def _extract_openai_image(
        self, b64_image: str, mime_type: str, context: Optional[str] = None
    ) -> LLMResult:
        """Use OpenAI GPT-4o vision API."""
        if not settings.OPENAI_API_KEY:
            return LLMResult(
                raw_response="",
                parsed_data={},
                provider="openai",
                model="gpt-4o",
                success=False,
                error="OPENAI_API_KEY not configured",
            )

        prompt = EXTRACTION_PROMPT
        if context:
            prompt += f"\n\nAdditional context:\n{context}"

        payload = {
            "model": "gpt-4o",
            "max_tokens": 4096,
            "response_format": {"type": "json_object"},
            "messages": [
                {
                    "role": "user",
                    "content": [
                        {
                            "type": "image_url",
                            "image_url": {
                                "url": f"data:{mime_type};base64,{b64_image}",
                                "detail": "high",
                            },
                        },
                        {"type": "text", "text": prompt},
                    ],
                }
            ],
        }

        try:
            with httpx.Client(timeout=60.0) as client:
                resp = client.post(
                    "https://api.openai.com/v1/chat/completions",
                    json=payload,
                    headers={
                        "Authorization": f"Bearer {settings.OPENAI_API_KEY}",
                        "Content-Type": "application/json",
                    },
                )
                resp.raise_for_status()

            data = resp.json()
            raw_text = data["choices"][0]["message"]["content"]
            parsed = self._parse_json_response(raw_text)

            return LLMResult(
                raw_response=raw_text,
                parsed_data=parsed,
                provider="openai",
                model="gpt-4o",
                success=True,
            )

        except Exception as e:
            logger.error(f"OpenAI extraction failed: {e}")
            return LLMResult(
                raw_response="",
                parsed_data={},
                provider="openai",
                model="gpt-4o",
                success=False,
                error=str(e),
            )

    def _extract_openai_text(self, text: str, prompt: str) -> LLMResult:
        """Use OpenAI GPT-4o text API."""
        if not settings.OPENAI_API_KEY:
            return LLMResult(
                raw_response="",
                parsed_data={},
                provider="openai",
                model="gpt-4o",
                success=False,
                error="OPENAI_API_KEY not configured",
            )

        payload = {
            "model": "gpt-4o",
            "max_tokens": 4096,
            "response_format": {"type": "json_object"},
            "messages": [
                {
                    "role": "user",
                    "content": f"{prompt}\n\nInvoice text:\n{text}",
                }
            ],
        }

        try:
            with httpx.Client(timeout=60.0) as client:
                resp = client.post(
                    "https://api.openai.com/v1/chat/completions",
                    json=payload,
                    headers={
                        "Authorization": f"Bearer {settings.OPENAI_API_KEY}",
                        "Content-Type": "application/json",
                    },
                )
                resp.raise_for_status()

            data = resp.json()
            raw_text = data["choices"][0]["message"]["content"]
            parsed = self._parse_json_response(raw_text)

            return LLMResult(
                raw_response=raw_text,
                parsed_data=parsed,
                provider="openai",
                model="gpt-4o",
                success=True,
            )

        except Exception as e:
            logger.error(f"OpenAI text extraction failed: {e}")
            return LLMResult(
                raw_response="",
                parsed_data={},
                provider="openai",
                model="gpt-4o",
                success=False,
                error=str(e),
            )

    def _parse_json_response(self, text: str) -> dict:
        """Parse JSON from LLM response, handling markdown code fences."""
        cleaned = text.strip()

        # Strip markdown code fences
        if cleaned.startswith("```"):
            lines = cleaned.split("\n")
            # Remove first and last lines (fences)
            lines = [l for l in lines if not l.strip().startswith("```")]
            cleaned = "\n".join(lines)

        try:
            return json.loads(cleaned)
        except json.JSONDecodeError:
            # Try to find JSON object in the text
            start = cleaned.find("{")
            end = cleaned.rfind("}") + 1
            if start >= 0 and end > start:
                try:
                    return json.loads(cleaned[start:end])
                except json.JSONDecodeError:
                    pass

            logger.warning("Failed to parse LLM response as JSON")
            return {}
