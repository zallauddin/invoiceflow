"""Application configuration loaded from environment variables."""

from pydantic_settings import BaseSettings
from typing import List


class Settings(BaseSettings):
    # Application
    APP_NAME: str = "InvoiceFlow"
    APP_VERSION: str = "0.1.0"
    DEBUG: bool = False
    SECRET_KEY: str = "change-me-in-production"

    # Database
    DATABASE_URL: str = "postgresql+asyncpg://invoiceflow:invoiceflow@localhost:5432/invoiceflow"
    DATABASE_ECHO: bool = False

    # Redis
    REDIS_URL: str = "redis://localhost:6379/0"

    # MinIO / S3
    MINIO_ENDPOINT: str = "localhost:9000"
    MINIO_ACCESS_KEY: str = "minioadmin"
    MINIO_SECRET_KEY: str = "minioadmin"
    MINIO_BUCKET: str = "invoiceflow"
    MINIO_SECURE: bool = False

    # Celery
    CELERY_BROKER_URL: str = "redis://localhost:6379/1"
    CELERY_RESULT_BACKEND: str = "redis://localhost:6379/2"

    # JWT Auth
    JWT_SECRET_KEY: str = "jwt-change-me-in-production"
    JWT_ALGORITHM: str = "HS256"
    JWT_EXPIRATION_MINUTES: int = 60

    # LLM Provider
    LLM_PROVIDER: str = "claude"  # "claude" or "openai"
    ANTHROPIC_API_KEY: str = ""
    OPENAI_API_KEY: str = ""
    LLM_CONFIDENCE_THRESHOLD: float = 0.85

    # CORS
    CORS_ORIGINS: List[str] = ["http://localhost:3000", "http://localhost:5173"]

    # Email (IMAP)
    IMAP_HOST: str = ""
    IMAP_PORT: int = 993
    IMAP_USERNAME: str = ""
    IMAP_PASSWORD: str = ""
    IMAP_FOLDER: str = "INBOX"

    # FTP/SFTP
    FTP_HOST: str = ""
    FTP_PORT: int = 21
    FTP_USERNAME: str = ""
    FTP_PASSWORD: str = ""
    SFTP_HOST: str = ""
    SFTP_PORT: int = 22
    SFTP_USERNAME: str = ""
    SFTP_PASSWORD: str = ""

    # Compliance
    COMPLIANCE_SANDBOX: bool = True
    PEPPOL_CUSTOMISATION_ID: str = ""
    ZATCA_API_KEY: str = ""
    ZATCA_API_SECRET: str = ""
    BRAZIL_CNPJ: str = ""
    INDIA_GSP_API_KEY: str = ""
    INDIA_GSP_API_SECRET: str = ""
    MX_PAC_API_KEY: str = ""
    MX_PAC_API_SECRET: str = ""
    ARCHIVE_PATH: str = "/data/archives"

    # ERP Connectors
    XERO_CLIENT_ID: str = ""
    XERO_CLIENT_SECRET: str = ""
    XERO_REDIRECT_URI: str = "http://localhost:8000/api/v1/connectors/xero/callback"
    SAP_API_KEY: str = ""
    SAP_API_SECRET: str = ""
    SAP_CLIENT: str = "100"
    ORACLE_API_KEY: str = ""
    ORACLE_API_SECRET: str = ""
    ERP_SANDBOX: bool = True

    class Config:
        env_file = ".env"
        env_file_encoding = "utf-8"


settings = Settings()
