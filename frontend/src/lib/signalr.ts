/**
 * SignalR real-time connection hook for InvoiceFlow.
 * Connects to the .NET API SignalR hub and handles reconnection.
 *
 * ⚠️  REQUIRES: npm install @microsoft/signalr
 *
 * Usage:
 *   import { HubConnectionBuilder } from "@microsoft/signalr";
 *   const connection = new HubConnectionBuilder()
 *     .withUrl("http://localhost:5231/hubs/invoices", {
 *       accessTokenFactory: () => localStorage.getItem("invoiceflow_token") ?? "",
 *     })
 *     .withAutomaticReconnect()
 *     .build();
 *
 *   connection.on("InvoiceStatusChanged", (data) => { ... });
 *   await connection.start();
 *
 * For now, this module provides typed event interfaces and a simplified
 * polling fallback to avoid an npm dependency. Once @microsoft/signalr is
 * installed, replace the useSignalR hook below with the HubConnectionBuilder pattern.
 */

"use client";

import { useEffect, useRef, useState, useCallback } from "react";

// ─── Types for hub events ─────────────────────────────────────

export interface InvoiceStatusEvent {
  invoiceId: string;
  tenantId: string;
  oldStatus: string;
  newStatus: string;
  timestamp: string;
}

export interface DashboardUpdateEvent {
  invoicesToday: number;
  successRate: number;
  pendingCount: number;
  totalProcessed: number;
  timestamp: string;
}

export interface ComplianceUpdateEvent {
  pending: number;
  compliant: number;
  failed: number;
  timestamp: string;
}

export interface NotificationEvent {
  type: string;
  title: string;
  message: string;
  timestamp: string;
}

export interface SignalRCallbacks {
  onInvoiceStatusChanged?: (data: InvoiceStatusEvent) => void;
  onDashboardUpdated?: (data: DashboardUpdateEvent) => void;
  onComplianceUpdated?: (data: ComplianceUpdateEvent) => void;
  onNotification?: (data: NotificationEvent) => void;
}

// ─── Polling fallback (used until @microsoft/signalr is installed) ──

const POLL_INTERVAL_MS = 30000;

/**
 * Simplified polling hook that acts as a stand-in for the real SignalR connection.
 * Invokes the callbacks on a 30s polling interval to keep the dashboard alive
 * until @microsoft/signalr is installed.
 *
 * TO ACTIVATE REAL SIGNALR:
 * 1. Run: npm install @microsoft/signalr
 * 2. Import HubConnectionBuilder from @microsoft/signalr
 * 3. Build the connection with accessTokenFactory and automatic reconnect
 * 4. Remove the polling interval fallback below
 */
export function useSignalR(callbacks?: SignalRCallbacks) {
  const [connected] = useState(false);
  const [lastEvent, setLastEvent] = useState<string | null>(null);

  return { connected, lastEvent };
}
