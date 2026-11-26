import { http, HttpResponse } from "msw";
import { setupServer } from "msw/node";
import type { DashboardSummary, Device, RouteFeature, Weighing, WorkoutSummary } from "../api/client";

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

export const workout = (over: Partial<WorkoutSummary> = {}): WorkoutSummary => ({
  id: "wk1",
  source: "samsung-health",
  sourceId: "uid-1:0",
  revision: 1,
  activityType: "RUNNING",
  startedAt: "2026-11-13T06:00:00Z",
  endedAt: "2026-11-13T07:00:00Z",
  utcOffsetMinutes: 60,
  activeDurationSeconds: 3000,
  distanceM: 10_000,
  energyKcal: 700,
  averageHeartRate: 142,
  maxHeartRate: 171,
  trackPointCount: 3600,
  bounds: { minLatitude: 41.89, minLongitude: 12.49, maxLatitude: 41.9, maxLongitude: 12.5 },
  updatedAt: "2026-11-13T07:05:00Z",
  ...over,
});

export const route: RouteFeature = {
  type: "Feature",
  bbox: [12.49, 41.89, 12.5, 41.9],
  geometry: {
    type: "LineString",
    coordinates: [
      [12.49, 41.89],
      [12.495, 41.895, 22],
      [12.5, 41.9],
    ],
  },
  properties: {
    workoutId: "wk1",
    totalPoints: 3600,
    returnedPoints: 3,
    hiddenStartPoints: 0,
    hiddenEndPoints: 0,
    times: [1, 2, 3],
  },
};

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
  http.get("*/api/v1/workouts", () =>
    HttpResponse.json({ items: [workout()], from: "2026-08-14T00:00:00Z", to: "2026-11-14T00:00:00Z" }),
  ),
  http.get("*/api/v1/workouts/:id", ({ params }) =>
    params.id === "wk1"
      ? HttpResponse.json({
          summary: workout(),
          series: [{ metric: "HeartRate", unit: "bpm", sampleCount: 3 }],
        })
      : HttpResponse.json({ title: "Not Found" }, { status: 404 }),
  ),
  http.get("*/api/v1/workouts/:id/route", () => HttpResponse.json(route)),
  http.get("*/api/v1/workouts/:id/series", () =>
    HttpResponse.json({
      items: [
        {
          metric: "HeartRate",
          unit: "bpm",
          times: [
            Date.parse("2026-11-13T06:00:00Z"),
            Date.parse("2026-11-13T06:00:01Z"),
            Date.parse("2026-11-13T06:00:02Z"),
          ],
          values: [120, 125, 130],
        },
      ],
    }),
  ),
  http.get("*/api/v1/workouts/:id/science", () =>
    HttpResponse.json({
      workoutId: "wk1",
      revision: 1,
      contentSha256: "synthetic",
      source: "samsung-health",
      asOf: "2026-11-13T07:05:00Z",
      methodVersion: "workout-channel-quality-v1",
      maxGapSeconds: 5,
      channels: [
        { metric: "HeartRate", unit: "bpm", status: "observed", sampleCount: 3, validCount: 3,
          conflictingCount: 0, coverage: 0.5, timeWeightedMean: 140 },
        { metric: "Power", unit: "W", status: "missing", sampleCount: 0, validCount: 0,
          conflictingCount: 0, coverage: 0, timeWeightedMean: null },
      ],
    }),
  ),
  http.get("*/api/v1/maps/config", () => HttpResponse.json({ provider: "none", googleApiKey: null })),
];

export const server = setupServer(...handlers);
