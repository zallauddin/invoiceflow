"""Common Pydantic schemas."""

from datetime import datetime
from typing import Optional, Any
from uuid import UUID

from pydantic import BaseModel


class BaseSchema(BaseModel):
    """Base schema with common fields."""
    class Config:
        from_attributes = True


class PaginationParams(BaseModel):
    page: int = 1
    page_size: int = 50


class PaginatedResponse(BaseModel):
    items: list[Any]
    total: int
    page: int
    page_size: int
    pages: int


class ErrorResponse(BaseModel):
    detail: str
    code: str | None = None


class SuccessResponse(BaseModel):
    message: str
    id: UUID | None = None
