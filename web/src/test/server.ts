import { http, HttpResponse } from "msw";
import { setupServer } from "msw/node";
import type { DashboardSummary, Device, Weighing } from "../api/client";

export const weighing = (over: Partial<Weighing> = {}): Weighing => ({
  id: crypto.randomUUID(),
  deviceId: "d1",
  sourceEventId: "evt",
  measuredAt: "2026-11-12T06:30:00Z",
  weightKg: 78.4,
  impedanceOhm: 480,
  attribution: "Assumed",
  receivedAt: "2026-11-12T06:30:05Z",
  ...over,
});

export const summary: DashboardSummary = {
  latestWeightKg: 78.4,
  latestMeasuredAt: "2026-11-12T06:30:00Z",
  trend7Days: -0.6,
  trend30Days: -1.8,
  weighingsLast30Days: 21,
  sources: [
    {
      deviceId: "d1",
      name: "Bilancia bagno",
      lastSeenAt: "2026-11-12T06:30:05Z",
      lastIngestedAt: "2026-11-12T06:30:05Z",
    },
  ],
};

export const device = (over: Partial<Device> = {}): Device => ({
  id: crypto.randomUUID(),
  name: "Bilancia bagno",
  kind: "ScaleAgent",
  createdAt: "2026-11-01T10:00:00Z",
  lastSeenAt: "2026-11-12T06:30:05Z",
  lastIngestedAt: "2026-11-12T06:30:05Z",
  revokedAt: null,
  ...over,
});

/** Handler di base: utente autenticato con dati realistici. I test li sovrascrivono con server.use(). */
export const handlers = [
  http.get("*/api/auth/me", () =>
    HttpResponse.json({ id: "u1", email: "stefano@example.it", displayName: null }),
  ),
  http.get("*/api/v1/dashboard/summary", () => HttpResponse.json(summary)),
  http.get("*/api/v1/weighings", () =>
    HttpResponse.json({
      items: [
        weighing({ id: "w2", measuredAt: "2026-11-12T06:30:00Z", weightKg: 78.4 }),
        weighing({ id: "w1", measuredAt: "2026-11-11T06:30:00Z", weightKg: 79 }),
      ],
      from: "2026-08-14T00:00:00Z",
      to: "2026-11-12T07:00:00Z",
    }),
  ),
  http.get("*/api/v1/devices", () => HttpResponse.json([device({ id: "d1" })])),
];

export const server = setupServer(...handlers);
