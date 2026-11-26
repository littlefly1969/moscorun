import type { Schemas } from "../../api/client";

export interface Sample {
  time: number;
  value: number;
}

export function observedSamples(series: Schemas["SeriesDto"]): Sample[] {
  return series.times
    .flatMap((time, index) => {
      const value = series.values[index];
      return Number.isFinite(time) && value != null && Number.isFinite(value) ? [{ time, value }] : [];
    })
    .sort((a, b) => a.time - b.time);
}

export function nearestSample(samples: Sample[], time: number, toleranceMs = 5000): Sample | undefined {
  let nearest: Sample | undefined;
  for (const sample of samples) {
    if (Math.abs(sample.time - time) > toleranceMs) continue;
    if (!nearest || Math.abs(sample.time - time) < Math.abs(nearest.time - time)) nearest = sample;
  }
  return nearest;
}

export function windowBounds(start: number, end: number, cursor: number, minutes: number) {
  const width = Math.min(end - start, minutes > 0 ? minutes * 60000 : end - start);
  const left = Math.max(start, Math.min(cursor - width / 2, end - width));
  return { left, right: left + width };
}
