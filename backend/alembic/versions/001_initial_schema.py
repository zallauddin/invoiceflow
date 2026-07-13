"""initial schema

Revision ID: 001_initial
Revises:
Create Date: 2026-06-01
"""

from alembic import op
import sqlalchemy as sa
from sqlalchemy.dialects import postgresql

revision = "001_initial"
down_revision = None
branch_labels = None
depends_on = None


def upgrade() -> None:
    # Tenants
    op.create_table(
        "tenants",
        sa.Column("id", postgresql.UUID(as_uuid=True), primary_key=True),
        sa.Column("name", sa.String(255), nullable=False),
        sa.Column("slug", sa.String(100), unique=True, nullable=False, index=True),
        sa.Column("country_code", sa.String(2), nullable=False),
        sa.Column("tax_id", sa.String(50), nullable=True),
        sa.Column("registration_number", sa.String(100), nullable=True),
        sa.Column("default_currency", sa.String(3), server_default="USD"),
        sa.Column("default_compliance_model", sa.String(20), server_default="peppol"),
        sa.Column("settings", postgresql.JSON, server_default="{}"),
        sa.Column("is_active", sa.Boolean, server_default="true", nullable=False),
        sa.Column("created_at", sa.DateTime, server_default=sa.func.now(), nullable=False),
        sa.Column("updated_at", sa.DateTime, server_default=sa.func.now(), nullable=False),
    )

    # Users
    op.create_table(
        "users",
        sa.Column("id", postgresql.UUID(as_uuid=True), primary_key=True),
        sa.Column("tenant_id", postgresql.UUID(as_uuid=True), sa.ForeignKey("tenants.id"), nullable=False, index=True),
        sa.Column("email", sa.String(255), unique=True, nullable=False, index=True),
        sa.Column("hashed_password", sa.String(255), nullable=False),
        sa.Column("full_name", sa.String(255), nullable=True),
        sa.Column("role", sa.String(20), server_default="user", nullable=False),
        sa.Column("is_active", sa.Boolean, server_default="true", nullable=False),
        sa.Column("created_at", sa.DateTime, server_default=sa.func.now(), nullable=False),
        sa.Column("updated_at", sa.DateTime, server_default=sa.func.now(), nullable=False),
    )

    # Invoices
    op.create_table(
        "invoices",
        sa.Column("id", postgresql.UUID(as_uuid=True), primary_key=True),
        sa.Column("tenant_id", postgresql.UUID(as_uuid=True), sa.ForeignKey("tenants.id"), nullable=False, index=True),
        sa.Column("status", sa.String(20), server_default="received", nullable=False, index=True),
        sa.Column("invoice_number", sa.String(100), nullable=False),
        sa.Column("vendor_name", sa.String(255), nullable=False),
        sa.Column("vendor_tax_id", sa.String(50), nullable=True),
        sa.Column("buyer_name", sa.String(255), nullable=True),
        sa.Column("buyer_tax_id", sa.String(50), nullable=True),
        sa.Column("invoice_date", sa.DateTime, nullable=True),
        sa.Column("due_date", sa.DateTime, nullable=True),
        sa.Column("currency", sa.String(3), server_default="USD", nullable=False),
        sa.Column("subtotal", sa.Float, server_default="0.0", nullable=False),
        sa.Column("tax_amount", sa.Float, server_default="0.0", nullable=False),
        sa.Column("total_amount", sa.Float, server_default="0.0", nullable=False),
        sa.Column("country_code", sa.String(2), nullable=False, index=True),
        sa.Column("compliance_model", sa.String(20), nullable=False),
        sa.Column("compliance_response", postgresql.JSON, server_default="{}"),
        sa.Column("source", sa.String(20), nullable=False),
        sa.Column("source_reference", sa.String(255), nullable=True),
        sa.Column("file_url", sa.String(500), nullable=True),
        sa.Column("original_filename", sa.String(255), nullable=True),
        sa.Column("mime_type", sa.String(100), nullable=True),
        sa.Column("ocr_confidence", sa.Float, nullable=True),
        sa.Column("extraction_method", sa.String(20), nullable=True),
        sa.Column("extracted_data", postgresql.JSON, server_default="{}"),
        sa.Column("processing_started_at", sa.DateTime, nullable=True),
        sa.Column("processing_completed_at", sa.DateTime, nullable=True),
        sa.Column("error_message", sa.Text, nullable=True),
        sa.Column("created_at", sa.DateTime, server_default=sa.func.now(), nullable=False),
        sa.Column("updated_at", sa.DateTime, server_default=sa.func.now(), nullable=False),
    )
    op.create_index("ix_invoices_tenant_status", "invoices", ["tenant_id", "status"])
    op.create_index("ix_invoices_tenant_country", "invoices", ["tenant_id", "country_code"])
    op.create_index("ix_invoices_created", "invoices", ["created_at"])

    # Invoice Lines
    op.create_table(
        "invoice_lines",
        sa.Column("id", postgresql.UUID(as_uuid=True), primary_key=True),
        sa.Column("invoice_id", postgresql.UUID(as_uuid=True), sa.ForeignKey("invoices.id", ondelete="CASCADE"), nullable=False, index=True),
        sa.Column("line_number", sa.Integer, nullable=False),
        sa.Column("description", sa.Text, nullable=True),
        sa.Column("quantity", sa.Float, server_default="1.0", nullable=False),
        sa.Column("unit_price", sa.Float, server_default="0.0", nullable=False),
        sa.Column("tax_rate", sa.Float, server_default="0.0", nullable=False),
        sa.Column("tax_amount", sa.Float, server_default="0.0", nullable=False),
        sa.Column("line_total", sa.Float, server_default="0.0", nullable=False),
        sa.Column("item_code", sa.String(50), nullable=True),
    )
    op.create_index("ix_invoice_lines_invoice_number", "invoice_lines", ["invoice_id", "line_number"])

    # Compliance Configs
    op.create_table(
        "compliance_configs",
        sa.Column("id", postgresql.UUID(as_uuid=True), primary_key=True),
        sa.Column("tenant_id", postgresql.UUID(as_uuid=True), sa.ForeignKey("tenants.id"), nullable=False, index=True),
        sa.Column("country_code", sa.String(2), nullable=False),
        sa.Column("model", sa.String(20), nullable=False),
        sa.Column("enabled", sa.Boolean, server_default="true", nullable=False),
        sa.Column("config", postgresql.JSON, server_default="{}"),
        sa.Column("created_at", sa.DateTime, server_default=sa.func.now(), nullable=False),
        sa.Column("updated_at", sa.DateTime, server_default=sa.func.now(), nullable=False),
    )

    # Audit Logs
    op.create_table(
        "audit_logs",
        sa.Column("id", postgresql.UUID(as_uuid=True), primary_key=True),
        sa.Column("tenant_id", postgresql.UUID(as_uuid=True), sa.ForeignKey("tenants.id"), nullable=False, index=True),
        sa.Column("invoice_id", postgresql.UUID(as_uuid=True), sa.ForeignKey("invoices.id"), nullable=True, index=True),
        sa.Column("action", sa.String(100), nullable=False, index=True),
        sa.Column("details", postgresql.JSON, server_default="{}"),
        sa.Column("message", sa.Text, nullable=True),
        sa.Column("user_id", sa.String(100), nullable=True),
        sa.Column("ip_address", sa.String(45), nullable=True),
        sa.Column("timestamp", sa.DateTime, server_default=sa.func.now(), nullable=False, index=True),
    )


def downgrade() -> None:
    op.drop_table("audit_logs")
    op.drop_table("compliance_configs")
    op.drop_table("invoice_lines")
    op.drop_table("invoices")
    op.drop_table("users")
    op.drop_table("tenants")
