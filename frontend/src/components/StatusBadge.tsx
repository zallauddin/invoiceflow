/**
 * Invoice status badge component with semantic colors.
 */
import { Badge } from "@/components/ui";
import type { InvoiceStatus } from "@/lib/types";

const statusConfig: Record<InvoiceStatus, { label: string; variant: "default" | "primary" | "success" | "warning" | "danger" | "info" | "muted" }> = {
  received: { label: "Received", variant: "info" },
  processing: { label: "Processing", variant: "primary" },
  extracted: { label: "Extracted", variant: "primary" },
  reviewing: { label: "Review", variant: "warning" },
  approved: { label: "Approved", variant: "success" },
  compliant: { label: "Compliant", variant: "success" },
  transmitted: { label: "Transmitted", variant: "success" },
  failed: { label: "Failed", variant: "danger" },
  rejected: { label: "Rejected", variant: "danger" },
};

export function StatusBadge({ status }: { status: InvoiceStatus }) {
  const config = statusConfig[status] || { label: status, variant: "default" as const };
  return <Badge variant={config.variant}>{config.label}</Badge>;
}

/**
 * Country flag emoji from 2-letter code.
 */
const countryFlags: Record<string, string> = {
  SA: "🇸🇦", BR: "🇧🇷", IN: "🇮🇳", MX: "🇲🇽", DE: "🇩🇪", FR: "🇫🇷",
  IT: "🇮🇹", PL: "🇵🇱", US: "🇺🇸", GB: "🇬🇧", NL: "🇳🇱", ES: "🇪🇸",
  AT: "🇦🇹", BE: "🇧🇪", SE: "🇸🇪", DK: "🇩🇰", FI: "🇫🇮", NO: "🇳🇴",
  SG: "🇸🇬", AE: "🇦🇪", JP: "🇯🇵", CN: "🇨🇳", AU: "🇦🇺", CA: "🇨🇦",
};

export function CountryBadge({ code }: { code: string }) {
  const flag = countryFlags[code] || "🏳️";
  return (
    <Badge variant="muted">
      {flag} {code}
    </Badge>
  );
}

/**
 * Compliance model badge.
 */
const modelLabels: Record<string, string> = {
  peppol: "PEPPOL",
  clearance: "Clearance",
  ctc: "CTC",
  post_audit: "Post-Audit",
};

export function ComplianceModelBadge({ model }: { model: string }) {
  return <Badge variant="primary">{modelLabels[model] || model}</Badge>;
}
