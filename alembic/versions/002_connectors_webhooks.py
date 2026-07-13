"""Add ERP connector configs and webhook configs tables.

Revision ID: 002_connectors_webhooks
Revises: 001_initial_schema
Create Date: 2026-06-02
"""

from alembic import op
import sqlalchemy as sa
from sqlalchemy.dialects.postgresql import UUID

revision = "002_connectors_webhooks"
down_revision = "001_initial_schema"
branch_labels = None
depends_on = None


def upgrade() -> None:
    # ERP Connector Configs
    op.create_table(
        "erp_connector_configs",
        sa.Column("id", UUID(as_uuid=True), primary_key=True),
        sa.Column("tenant_id", UUID(as_uuid=True), sa.ForeignKey("tenants.id"), nullable=False),
        sa.Column("connector_type", sa.String(50), nullable=False),
        sa.Column("display_name", sa.String(100), nullable=False),
        sa.Column("status", sa.String(20), server_default="inactive"),
        sa.Column("api_key", sa.Text, nullable=True),
        sa.Column("api_secret", sa.Text, nullable=True),
        sa.Column("base_url", sa.String(500), nullable=True),
        sa.Column("sandbox", sa.Boolean, server_default="true"),
        sa.Column("sync_direction", sa.String(20), server_default="push"),
        sa.Column("extra_config", sa.JSON, server_default="{}"),
        sa.Column("access_token", sa.Text, nullable=True),
        sa.Column("refresh_token", sa.Text, nullable=True),
        sa.Column("token_expiry", sa.DateTime, nullable=True),
        sa.Column("last_sync_at", sa.DateTime, nullable=True),
        sa.Column("last_sync_result", sa.JSON, server_default="{}"),
        sa.Column("total_synced", sa.Integer, server_default="0"),
        sa.Column("total_failed", sa.Integer, server_default="0"),
        sa.Column("created_at", sa.DateTime, nullable=False, server_default=sa.func.now()),
        sa.Column("updated_at", sa.DateTime, nullable=False, server_default=sa.func.now()),
    )
    op.create_index(
        "ix_erp_configs_tenant_type",
        "erp_connector_configs",
        ["tenant_id", "connector_type"],
        unique=True,
    )

    # Webhook Configs
    op.create_table(
        "webhook_configs",
        sa.Column("id", UUID(as_uuid=True), primary_key=True),
        sa.Column("tenant_id", UUID(as_uuid=True), sa.ForeignKey("tenants.id"), nullable=False),
        sa.Column("name", sa.String(100), nullable=False),
        sa.Column("url", sa.String(500), nullable=False),
        sa.Column("secret", sa.String(255), nullable=True),
        sa.Column("active", sa.Boolean, server_default="true"),
        sa.Column("events", sa.JSON, server_default="[]"),
        sa.Column("content_type", sa.String(50), server_default="application/json"),
        sa.Column("timeout_seconds", sa.Integer, server_default="30"),
        sa.Column("max_retries", sa.Integer, server_default="3"),
        sa.Column("last_triggered_at", sa.DateTime, nullable=True),
        sa.Column("last_status_code", sa.Integer, nullable=True),
        sa.Column("success_count", sa.Integer, server_default="0"),
        sa.Column("failure_count", sa.Integer, server_default="0"),
        sa.Column("created_at", sa.DateTime, nullable=False, server_default=sa.func.now()),
        sa.Column("updated_at", sa.DateTime, nullable=False, server_default=sa.func.now()),
    )
    op.create_index("ix_webhook_configs_tenant", "webhook_configs", ["tenant_id"])


def downgrade() -> None:
    op.drop_table("webhook_configs")
    op.drop_table("erp_connector_configs")
