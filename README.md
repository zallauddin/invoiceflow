# InvoiceFlow

AI-powered e-invoice processing platform with global compliance support.

## Features

- **Multi-source ingestion**: Email (IMAP), FTP/SFTP, REST API, file upload
- **AI extraction**: Hybrid OCR (Tesseract) + LLM fallback (Claude/GPT)
- **Format support**: PDF, images, XML (UBL, CII, Factur-X/ZUGFeRD)
- **Global compliance**: PEPPOL, Clearance (ZATCA, Brazil, India, Mexico), CTC
- **Web dashboard**: Invoice management, review, approval, analytics

## Quick Start

```bash
# Clone and configure
cp .env.example .env
# Edit .env with your settings

# Start all services
docker-compose up -d

# Access
# Frontend: http://localhost:3000
# Backend API: http://localhost:8000
# API docs: http://localhost:8000/docs
# MinIO console: http://localhost:9001
```

## Architecture

```
Email/FTP/API/Webhook → Ingestion → AI Pipeline → Compliance Engine → Output
                                      ↓
                               PostgreSQL + MinIO
                                      ↓
                              React Dashboard
```

## Development

```bash
# Backend only
cd backend && pip install -r requirements.txt
uvicorn app.main:app --reload

# Frontend only
cd frontend && npm install && npm run dev
```

## Tech Stack

- **Backend**: Python 3.12, FastAPI, SQLAlchemy, Celery
- **Frontend**: Next.js 14, React 18, TypeScript, Tailwind CSS
- **Database**: PostgreSQL 16, Redis 7
- **Storage**: MinIO (S3-compatible)
- **AI**: Tesseract OCR, Claude/GPT API
