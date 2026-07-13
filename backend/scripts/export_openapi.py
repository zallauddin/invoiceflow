"""Export the FastAPI OpenAPI schema to a JSON file for frontend type generation.

Usage:
    python backend/scripts/export_openapi.py [--output path/to/openapi.json]

The output file is consumed by `openapi-typescript` to generate TypeScript types.
"""
import json
import sys
from pathlib import Path

# Add the backend to the path so we can import the app
backend_dir = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(backend_dir))

from app.main import app


def main():
    output_path = sys.argv[1] if len(sys.argv) > 1 else "frontend/src/lib/openapi.json"

    # Generate the OpenAPI schema
    openapi_schema = app.openapi()
    openapi_schema["info"]["description"] = (
        "InvoiceFlow API — E-invoice processing platform with AI extraction "
        "and global compliance (PEPPOL, ZATCA, Brazil NFe, India IRP, Mexico CFDI, "
        "CTC reporting, and post-audit archival)."
    )
    openapi_schema["info"]["version"] = "0.2.0"

    # Add server URLs
    openapi_schema["servers"] = [
        {"url": "http://localhost:8000", "description": "Local development"},
        {"url": "/api/v1", "description": "Production (Nginx proxy)"},
    ]

    # Write to file
    output_file = Path(output_path)
    output_file.parent.mkdir(parents=True, exist_ok=True)
    with open(output_file, "w") as f:
        json.dump(openapi_schema, f, indent=2)

    print(f"OpenAPI schema exported to {output_file}")
    print(f"  Paths: {len(openapi_schema.get('paths', {}))}")
    print(f"  Schemas: {len(openapi_schema.get('components', {}).get('schemas', {}))}")


if __name__ == "__main__":
    main()
