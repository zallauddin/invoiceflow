/**
 * InvoiceFlow i18n — lightweight multi-language support.
 *
 * Usage:
 *   import { useI18n, I18nProvider } from "@/lib/i18n";
 *   const { t, setLocale } = useI18n();
 *   t("dashboard.title")        // → "Dashboard"
 *   setLocale("ar");            // switch to Arabic
 *
 * Standalone (non-React):
 *   import { tStatic, setLocale } from "@/lib/i18n";
 *   tStatic("dashboard.title");
 */

"use client";

import { createContext, useContext, useState, useCallback, type ReactNode } from "react";

// ─── Supported Locales ──────────────────────────────────────

export type Locale = "en" | "ar" | "fr" | "de" | "es" | "pt" | "zh" | "ja";

export const SUPPORTED_LOCALES: { code: Locale; label: string; flag: string }[] = [
  { code: "en", label: "English", flag: "🇬🇧" },
  { code: "ar", label: "العربية", flag: "🇸🇦" },
  { code: "fr", label: "Français", flag: "🇫🇷" },
  { code: "de", label: "Deutsch", flag: "🇩🇪" },
  { code: "es", label: "Español", flag: "🇪🇸" },
  { code: "pt", label: "Português", flag: "🇧🇷" },
  { code: "zh", label: "中文", flag: "🇨🇳" },
  { code: "ja", label: "日本語", flag: "🇯🇵" },
];

// ─── Translation Dict (extensible) ──────────────────────────

type TranslationDict = Record<string, Record<string, string>>;

const translations: TranslationDict = {
  en: {
    "app.name": "InvoiceFlow",
    "app.tagline": "AI-powered e-invoice processing",
    "nav.dashboard": "Dashboard",
    "nav.invoices": "Invoices",
    "nav.connectors": "Connectors",
    "nav.webhooks": "Webhooks",
    "nav.settings": "Settings",
    "dashboard.title": "Dashboard",
    "dashboard.needsAttention": "Needs Your Attention",
    "dashboard.needReview": "Need Review",
    "dashboard.failed": "Failed / Errors",
    "dashboard.pendingCompliance": "Pending Compliance",
    "dashboard.invoicesToday": "Invoices Today",
    "dashboard.successRate": "Success Rate",
    "dashboard.totalProcessed": "Total Processed",
    "dashboard.complianceReady": "Compliance Ready",
    "dashboard.noData": "No data yet - process some invoices to see charts",
    "dashboard.dailyVolume": "Daily Invoice Volume",
    "invoices.title": "Invoices",
    "invoices.total": "total invoices",
    "invoices.upload": "Upload Invoice",
    "invoices.noResults": "No invoices found",
    "invoices.noResultsDesc": "Try adjusting your filters or upload a new invoice.",
    "invoices.search": "Search by invoice number, vendor...",
    "invoices.allStatuses": "All Statuses",
    "invoices.allCountries": "All Countries",
    "invoices.clearFilters": "Clear filters",
    "invoices.selected": "selected",
    "invoices.clearSelection": "Clear selection",
    "invoices.approveAll": "Approve All",
    "invoices.rejectAll": "Reject All",
    "invoices.complyAll": "Comply All",
    "invoices.transmitAll": "Transmit All",
    "common.cancel": "Cancel",
    "common.save": "Save",
    "common.create": "Create",
    "common.delete": "Delete",
    "common.edit": "Edit",
    "common.loading": "Loading...",
    "common.error": "Error",
    "common.success": "Success",
    "common.dismiss": "Dismiss",
    "auth.login": "Sign In",
    "auth.email": "Email",
    "auth.password": "Password",
    "auth.signIn": "Sign in to your account",
  },
  ar: {
    "app.name": "InvoiceFlow",
    "app.tagline": "معالجة الفواتير الإلكترونية بالذكاء الاصطناعي",
    "nav.dashboard": "لوحة القيادة",
    "nav.invoices": "الفواتير",
    "dashboard.title": "لوحة القيادة",
    "dashboard.needReview": "تحتاج مراجعة",
    "dashboard.failed": "فشل / أخطاء",
    "dashboard.invoicesToday": "فواتير اليوم",
    "dashboard.successRate": "معدل النجاح",
    "invoices.title": "الفواتير",
    "invoices.total": "إجمالي الفواتير",
    "invoices.upload": "رفع فاتورة",
    "common.cancel": "إلغاء",
    "common.save": "حفظ",
    "common.loading": "جارٍ التحميل...",
    "auth.login": "تسجيل الدخول",
    "auth.email": "البريد الإلكتروني",
    "auth.password": "كلمة المرور",
  },
  fr: {
    "app.name": "InvoiceFlow",
    "app.tagline": "Traitement de factures electroniques par IA",
    "nav.dashboard": "Tableau de bord",
    "nav.invoices": "Factures",
    "dashboard.title": "Tableau de bord",
    "dashboard.needReview": "A verifier",
    "dashboard.failed": "Echecs / Erreurs",
    "dashboard.invoicesToday": "Factures aujourd'hui",
    "dashboard.successRate": "Taux de reussite",
    "invoices.title": "Factures",
    "invoices.total": "factures au total",
    "invoices.upload": "Telecharger une facture",
    "common.cancel": "Annuler",
    "common.save": "Enregistrer",
    "common.loading": "Chargement...",
    "auth.login": "Connexion",
    "auth.email": "E-mail",
    "auth.password": "Mot de passe",
  },
  de: {
    "app.name": "InvoiceFlow",
    "app.tagline": "KI-gestutzte E-Rechnungsverarbeitung",
    "nav.dashboard": "Dashboard",
    "nav.invoices": "Rechnungen",
    "dashboard.title": "Dashboard",
    "dashboard.needReview": "Uberprufung erforderlich",
    "dashboard.failed": "Fehlgeschlagen / Fehler",
    "dashboard.invoicesToday": "Rechnungen heute",
    "dashboard.successRate": "Erfolgsrate",
    "invoices.title": "Rechnungen",
    "invoices.total": "Rechnungen insgesamt",
    "invoices.upload": "Rechnung hochladen",
    "common.cancel": "Abbrechen",
    "common.save": "Speichern",
    "common.loading": "Laden...",
    "auth.login": "Anmelden",
    "auth.email": "E-Mail",
    "auth.password": "Passwort",
  },
};

// ─── Context-based i18n ─────────────────────────────────────

interface I18nContextValue {
  locale: Locale;
  setLocale: (locale: Locale) => void;
  t: (key: string, fallback?: string) => string;
}

const I18nContext = createContext<I18nContextValue | null>(null);

export function useI18n() {
  const ctx = useContext(I18nContext);
  if (!ctx) throw new Error("useI18n must be used within <I18nProvider>");
  return ctx;
}

export function I18nProvider({ children }: { children: ReactNode }) {
  const [locale, setLocaleState] = useState<Locale>("en");

  const setLocaleCtx = useCallback((newLocale: Locale) => {
    setLocaleState(newLocale);
    document.documentElement.lang = newLocale;
    document.documentElement.dir = newLocale === "ar" ? "rtl" : "ltr";
  }, []);

  const tCtx = useCallback(
    (key: string, fallback?: string) => {
      return translations[locale]?.[key] ?? fallback ?? key;
    },
    [locale]
  );

  return (
    <I18nContext.Provider value={{ locale, setLocale: setLocaleCtx, t: tCtx }}>
      {children}
    </I18nContext.Provider>
  );
}

// ─── Standalone helpers (for non-React usage) ───────────────

let _currentLocale: Locale = "en";

export function tStatic(key: string, fallback?: string): string {
  return translations[_currentLocale]?.[key] ?? fallback ?? key;
}

export function setLocaleStatic(locale: Locale) {
  _currentLocale = locale;
  if (typeof document !== "undefined") {
    document.documentElement.lang = locale;
    document.documentElement.dir = locale === "ar" ? "rtl" : "ltr";
  }
}

export function getCurrentLocale(): Locale {
  return _currentLocale;
}
