const kg = new Intl.NumberFormat("it-IT", { minimumFractionDigits: 1, maximumFractionDigits: 2 });
const delta = new Intl.NumberFormat("it-IT", {
  minimumFractionDigits: 1,
  maximumFractionDigits: 2,
  signDisplay: "exceptZero",
});
const dateTime = new Intl.DateTimeFormat("it-IT", { dateStyle: "medium", timeStyle: "short" });
const shortDate = new Intl.DateTimeFormat("it-IT", { day: "2-digit", month: "short" });
const relative = new Intl.RelativeTimeFormat("it-IT", { numeric: "auto" });

export const formatKg = (value: number | null | undefined): string =>
  value == null ? "—" : `${kg.format(value)} kg`;

export const formatDeltaKg = (value: number | null | undefined): string =>
  value == null ? "—" : `${delta.format(value)} kg`;

export const formatDateTime = (iso: string): string => dateTime.format(new Date(iso));

export const formatShortDate = (epochMs: number): string => shortDate.format(new Date(epochMs));

const units: [Intl.RelativeTimeFormatUnit, number][] = [
  ["day", 86_400_000],
  ["hour", 3_600_000],
  ["minute", 60_000],
];

/** "3 ore fa", "ieri"... Restituisce "mai" se il dato non è mai arrivato. */
export function formatRelative(iso: string | null | undefined, now: Date = new Date()): string {
  if (!iso) return "mai";
  const diff = new Date(iso).getTime() - now.getTime();
  for (const [unit, ms] of units) {
    if (Math.abs(diff) >= ms) return relative.format(Math.round(diff / ms), unit);
  }
  return "adesso";
}

export type Freshness = "fresh" | "stale" | "missing";

/** Freschezza di una sorgente: oltre 48 ore senza dati è un segnale da mostrare. */
export function freshness(iso: string | null | undefined, now: Date = new Date()): Freshness {
  if (!iso) return "missing";
  return now.getTime() - new Date(iso).getTime() > 48 * 3_600_000 ? "stale" : "fresh";
}
