import createClient from "openapi-fetch";
import type { components, paths } from "./schema";

export type Schemas = components["schemas"];
export type Weighing = Schemas["WeighingDto"];
export type WeighingList = Schemas["WeighingList"];
export type DashboardSummary = Schemas["DashboardSummary"];
export type Device = Schemas["DeviceDto"];
export type DeviceKind = Schemas["DeviceKind"];
export type RegisteredDevice = Schemas["RegisteredDevice"];
export type AttributionStatus = Schemas["AttributionStatus"];
export type CurrentUser = Schemas["CurrentUser"];

/** Errore HTTP con lo stato e il ProblemDetails (RFC 9457) restituito dal backend. */
export class ApiError extends Error {
  constructor(
    readonly status: number,
    readonly problem?: { title?: string | null; detail?: string | null; errors?: Record<string, string[]> },
  ) {
    super(problem?.detail ?? problem?.title ?? `Errore HTTP ${status}`);
    this.name = "ApiError";
  }
}

/** Client tipizzato generato dal contratto OpenAPI versionato in /contracts. */
export const api = createClient<paths>({
  baseUrl: typeof window === "undefined" ? "http://localhost" : window.location.origin,
  credentials: "same-origin",
  // Risolto a ogni chiamata: permette a strumenti (MSW nei test) di intercettare fetch dopo l'import.
  fetch: (request) => globalThis.fetch(request),
});

/** Restituisce i dati o lancia ApiError: semplifica l'uso con TanStack Query. */
export async function unwrap<T>(
  call: Promise<{ data?: T; error?: unknown; response: Response }>,
): Promise<T> {
  const { data, error, response } = await call;
  if (!response.ok) {
    throw new ApiError(response.status, error as ApiError["problem"]);
  }
  return data as T;
}
