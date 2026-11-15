import { projectRoute, type Position } from "../../lib/geo";

const width = 640;
const height = 400;

/** Percorso disegnato in locale, senza cartografia: nessuna richiesta esce dal server. */
export function RouteSketch({ coordinates }: { coordinates: readonly Position[] }) {
  const projected = projectRoute(coordinates, width, height, 20);
  if (!projected) return <p className="muted">Percorso troppo breve da disegnare.</p>;
  return (
    <svg
      className="route-sketch"
      viewBox={`0 0 ${width} ${height}`}
      role="img"
      aria-label="Percorso dell'allenamento (senza mappa di sfondo)"
      preserveAspectRatio="xMidYMid meet"
    >
      <path
        d={projected.path}
        fill="none"
        stroke="var(--accent)"
        strokeWidth={3}
        strokeLinejoin="round"
        strokeLinecap="round"
      />
      <circle cx={projected.start.x} cy={projected.start.y} r={7} className="route-start" />
      <circle cx={projected.end.x} cy={projected.end.y} r={7} className="route-end" />
    </svg>
  );
}
