"""Settings API routes."""

from fastapi import APIRouter, Depends
from sqlalchemy import select
from sqlalchemy.ext.asyncio import AsyncSession

from app.database import get_db
from app.api.deps import get_current_user, CurrentUser
from app.models.tenant import Tenant

router = APIRouter()


@router.get("/")
async def get_settings(
    db: AsyncSession = Depends(get_db),
    current_user: CurrentUser = Depends(get_current_user),
):
    """Get tenant settings."""
    result = await db.execute(
        select(Tenant).where(Tenant.id == current_user.tenant_id)
    )
    tenant = result.scalar_one_or_none()
    if not tenant:
        return {"settings": {}}

    return {
        "settings": {
            "tenant_name": tenant.name,
            "country_code": tenant.country_code,
            "default_currency": tenant.default_currency,
            "default_compliance_model": tenant.default_compliance_model,
            "tax_id": tenant.tax_id,
            "registration_number": tenant.registration_number,
            "custom_settings": tenant.settings or {},
        }
    }


@router.put("/")
async def update_settings(
    body: dict,
    db: AsyncSession = Depends(get_db),
    current_user: CurrentUser = Depends(get_current_user),
):
    """Update tenant settings."""
    result = await db.execute(
        select(Tenant).where(Tenant.id == current_user.tenant_id)
    )
    tenant = result.scalar_one_or_none()
    if not tenant:
        return {"updated": False, "error": "Tenant not found"}

    updatable = ["name", "country_code", "default_currency", "default_compliance_model", "tax_id", "registration_number"]
    for field in updatable:
        if field in body:
            setattr(tenant, field, body[field])

    if "custom_settings" in body:
        tenant.settings = {**tenant.settings, **body["custom_settings"]}

    await db.commit()
    return {"updated": True}


@router.get("/countries")
async def list_country_configs(
    current_user: CurrentUser = Depends(get_current_user),
):
    """List available country compliance configurations."""
    return {
        "countries": [
            {"code": "SA", "name": "Saudi Arabia", "model": "clearance", "system": "ZATCA FATOORAH"},
            {"code": "BR", "name": "Brazil", "model": "clearance", "system": "NFe v4.00"},
            {"code": "IN", "name": "India", "model": "clearance", "system": "GST e-Invoice IRP"},
            {"code": "MX", "name": "Mexico", "model": "clearance", "system": "CFDI 4.0"},
            {"code": "DE", "name": "Germany", "model": "peppol", "system": "XRechnung / PEPPOL"},
            {"code": "FR", "name": "France", "model": "ctc", "system": "PPF"},
            {"code": "IT", "name": "Italy", "model": "ctc", "system": "SdI"},
            {"code": "PL", "name": "Poland", "model": "ctc", "system": "KSeF"},
            {"code": "US", "name": "United States", "model": "post_audit", "system": "Post-Audit Archival"},
            {"code": "GB", "name": "United Kingdom", "model": "peppol", "system": "PEPPOL BIS Billing 3.0"},
            {"code": "NL", "name": "Netherlands", "model": "peppol", "system": "PEPPOL BIS Billing 3.0"},
            {"code": "ES", "name": "Spain", "model": "peppol", "system": "Facturae / PEPPOL"},
        ]
    }
