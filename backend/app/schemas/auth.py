"""Auth Pydantic schemas."""

from uuid import UUID
from pydantic import BaseModel, EmailStr


class TokenRequest(BaseModel):
    email: str
    password: str


class TokenResponse(BaseModel):
    access_token: str
    token_type: str = "bearer"
    user_id: str
    tenant_id: str


class RegisterRequest(BaseModel):
    email: EmailStr
    password: str
    full_name: str | None = None
    tenant_name: str
    tenant_slug: str
    country_code: str = "US"


class UserResponse(BaseModel):
    id: UUID
    email: str
    full_name: str | None = None
    role: str
    tenant_id: UUID

    class Config:
        from_attributes = True
