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

const decimal1 = new Intl.NumberFormat("it-IT", { minimumFractionDigits: 1, maximumFractionDigits: 2 });
const integer = new Intl.NumberFormat("it-IT", { maximumFractionDigits: 0 });

/** 1:02:05 oppure 42:10. */
export function formatDuration(seconds: number | null | undefined): string {
  if (seconds == null) return "—";
  const total = Math.round(seconds);
  const h = Math.floor(total / 3600);
  const m = Math.floor((total % 3600) / 60);
  const s = total % 60;
  const mm = String(m).padStart(h > 0 ? 2 : 1, "0");
  return `${h > 0 ? `${h}:` : ""}${mm}:${String(s).padStart(2, "0")}`;
}

export const formatDistance = (meters: number | null | undefined): string =>
  meters == null
    ? "—"
    : meters < 1000
      ? `${integer.format(meters)} m`
      : `${decimal1.format(meters / 1000)} km`;

/** Passo in min/km; serve una distanza di almeno 100 m per essere significativo. */
export function formatPace(seconds: number | null | undefined, meters: number | null | undefined): string {
  if (seconds == null || meters == null || meters < 100) return "—";
  const perKm = Math.round(seconds / (meters / 1000));
  return `${Math.floor(perKm / 60)}:${String(perKm % 60).padStart(2, "0")} /km`;
}

export const formatBpm = (value: number | null | undefined): string =>
  value == null ? "—" : `${integer.format(value)} bpm`;

const activityLabels: Record<string, string> = {
  RUNNING: "Corsa",
  WALKING: "Camminata",
  HIKING: "Escursione",
  CYCLING: "Bici",
  TREADMILL: "Tapis roulant",
  SWIMMING: "Nuoto",
};

/** Etichetta leggibile del codice della fonte; i codici sconosciuti restano riconoscibili. */
export function activityLabel(code: string): string {
  const known = activityLabels[code.toUpperCase()];
  if (known) return known;
  const words = code
    .replace(/[_.:-]+/g, " ")
    .trim()
    .toLowerCase();
  return words ? words.charAt(0).toUpperCase() + words.slice(1) : code;
}
