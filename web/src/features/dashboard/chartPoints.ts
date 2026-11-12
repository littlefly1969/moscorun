import type { Weighing } from "../../api/client";

export interface ChartPoint {
  t: number;
  kg: number;
}

/** Punti in ordine cronologico, escluse le pesate attribuite ad altri. */
export function toChartPoints(items: readonly Weighing[]): ChartPoint[] {
  return items
    .filter((w) => w.attribution !== "Rejected")
    .map((w) => ({ t: new Date(w.measuredAt).getTime(), kg: w.weightKg }))
    .sort((a, b) => a.t - b.t);
}
