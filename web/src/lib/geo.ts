/** Coordinata GeoJSON: [longitudine, latitudine] o [longitudine, latitudine, altitudine]. */
export type Position = readonly number[];

export interface Projected {
  readonly path: string;
  readonly start: { x: number; y: number };
  readonly end: { x: number; y: number };
}

/**
 * Proietta il tracciato in un riquadro SVG mantenendo le proporzioni reali (equirettangolare locale,
 * corretta per la latitudine): serve a disegnare il percorso senza cartografia e senza richieste esterne.
 */
export function projectRoute(
  coordinates: readonly Position[],
  width: number,
  height: number,
  padding = 12,
): Projected | null {
  if (coordinates.length < 2) return null;
  const lons = coordinates.map((c) => c[0] ?? 0);
  const lats = coordinates.map((c) => c[1] ?? 0);
  const minLon = Math.min(...lons);
  const maxLon = Math.max(...lons);
  const minLat = Math.min(...lats);
  const maxLat = Math.max(...lats);
  const kx = Math.cos((((minLat + maxLat) / 2) * Math.PI) / 180);
  const spanX = Math.max((maxLon - minLon) * kx, 1e-9);
  const spanY = Math.max(maxLat - minLat, 1e-9);
  const scale = Math.min((width - 2 * padding) / spanX, (height - 2 * padding) / spanY);
  const offsetX = (width - spanX * scale) / 2;
  const offsetY = (height - spanY * scale) / 2;
  const point = (c: Position) => ({
    x: round(offsetX + ((c[0] ?? 0) - minLon) * kx * scale),
    y: round(offsetY + (maxLat - (c[1] ?? 0)) * scale),
  });
  const points = coordinates.map(point);
  const path = points.map((p, i) => `${i === 0 ? "M" : "L"}${p.x} ${p.y}`).join(" ");
  const [start] = points;
  const end = points.at(-1);
  return start && end ? { path, start, end } : null;
}

const round = (v: number) => Math.round(v * 10) / 10;
