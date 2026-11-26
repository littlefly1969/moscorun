import { useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { Link } from "react-router";
import { api, unwrap, type Schemas, type WorkoutSummary } from "../../api/client";
import { formatDuration } from "../../lib/format";
import { observedSamples, nearestSample, windowBounds } from "./series";
import { WorkoutPicker } from "./WorkoutPicker";

const names: Record<string, string> = {
  HeartRate: "Frequenza cardiaca",
  Speed: "Velocità",
  Cadence: "Cadenza",
  Power: "Potenza",
};
const number = new Intl.NumberFormat("it-IT", { maximumFractionDigits: 2 });

function Trace({
  series,
  left,
  right,
  cursor,
}: {
  series: Schemas["SeriesDto"];
  left: number;
  right: number;
  cursor: number;
}) {
  const name = names[series.metric ?? ""] ?? "Serie non classificata";
  const samples = observedSamples(series);
  const points = samples.filter((sample) => sample.time >= left && sample.time <= right);
  const low = points.length > 0 ? Math.min(...points.map((sample) => sample.value)) : 0;
  const high = points.length > 0 ? Math.max(...points.map((sample) => sample.value)) : 0;
  const nearest = nearestSample(points, cursor);
  const x = (time: number) => 10 + (780 * (time - left)) / Math.max(1, right - left);
  const y = (value: number) => (high === low ? 42 : 70 - (56 * (value - low)) / (high - low));
  return (
    <section className="sw-trace" aria-label={name}>
      <div className="sw-trace-label">
        <h2>{name}</h2>
        <small>{series.unit}</small>
        <strong>{nearest ? number.format(nearest.value) : "—"}</strong>
        <small>
          {nearest
            ? "Campione a " + number.format((nearest.time - cursor) / 1000) + " s dal cursore"
            : "Nessun campione entro 5 s"}
        </small>
        <small>
          {samples.length}/{series.totalSamples} campioni restituiti
        </small>
      </div>
      <figure className="sw-figure">
        <svg viewBox="0 0 800 84" role="img" aria-label={name + " nella finestra temporale"}>
          {[0, 1, 2, 3, 4, 5, 6, 7, 8].map((i) => (
            <line
              key={i}
              x1={10 + i * 97.5}
              x2={10 + i * 97.5}
              y1="8"
              y2="76"
              stroke="currentColor"
              opacity=".12"
            />
          ))}
          {points.map((sample, index) => (
            <circle key={index} cx={x(sample.time)} cy={y(sample.value)} r="1.8" fill="currentColor" />
          ))}
          <line x1={x(cursor)} x2={x(cursor)} y1="0" y2="84" stroke="var(--sw-cursor)" strokeWidth="2" />
        </svg>
        <figcaption>
          {points.length > 0
            ? number.format(low) + " – " + number.format(high) + " " + series.unit
            : "Nessun campione nella finestra"}
        </figcaption>
      </figure>
    </section>
  );
}

function Timeline({ workout }: { workout: WorkoutSummary }) {
  const [offset, setOffset] = useState(0);
  const [minutes, setMinutes] = useState(0);
  const [hidden, setHidden] = useState<string[]>([]);
  const series = useQuery({
    queryKey: ["workspace", "series", workout.id, workout.revision],
    queryFn: () =>
      unwrap(
        api.GET("/api/v1/workouts/{id}/series", {
          params: { path: { id: workout.id }, query: { maxPoints: 2000 } },
        }),
      ),
  });
  const start = Date.parse(workout.startedAt);
  const end = Date.parse(workout.endedAt);
  const duration = Math.max(0, Math.floor((end - start) / 1000));
  const cursor = start + 1000 * Math.min(offset, duration);
  const bounds = windowBounds(start, end, cursor, minutes);
  if (series.isPending) return <p role="status">Caricamento delle serie…</p>;
  if (series.isError) return <p role="alert">Serie non disponibili.</p>;
  if (!Number.isFinite(duration) || duration <= 0) return <p>Intervallo temporale non valido.</p>;
  const items = series.data.items.filter((item) => item.metric != null);
  return (
    <>
      <p className="sw-muted">
        {workout.source} · revisione {workout.revision} ·{" "}
        <Link to={"/allenamenti/" + workout.id}>Dettaglio della seduta</Link>
      </p>
      <div className="sw-controls">
        <label>
          Finestra temporale
          <select value={minutes} onChange={(event) => setMinutes(Number(event.target.value))}>
            <option value={0}>Intera seduta</option>
            <option value={5}>5 minuti</option>
            <option value={15}>15 minuti</option>
          </select>
        </label>
        <label className="sw-scrubber">
          Cursore · {formatDuration(offset)} / {formatDuration(duration)}
          <input
            type="range"
            min={0}
            max={duration}
            value={offset}
            onChange={(event) => setOffset(Number(event.target.value))}
          />
        </label>
      </div>
      <div className="sw-controls">
        {items.map((item) => {
          const key = item.metric ?? "";
          return (
            <label className="sw-check" key={key}>
              <input
                type="checkbox"
                checked={!hidden.includes(key)}
                onChange={(event) =>
                  setHidden((current) =>
                    event.target.checked ? current.filter((name) => name !== key) : [...current, key],
                  )
                }
              />
              {names[key] ?? key}
            </label>
          );
        })}
      </div>
      <div className="sw-ruler">
        <span>{formatDuration((bounds.left - start) / 1000)}</span>
        <span>{formatDuration((bounds.right - start) / 1000)}</span>
      </div>
      {items.length === 0 && <p>Nessuna serie registrata per questa seduta.</p>}
      {items
        .filter((item) => !hidden.includes(item.metric ?? ""))
        .map((item) => (
          <Trace key={item.metric} series={item} left={bounds.left} right={bounds.right} cursor={cursor} />
        ))}
      <p className="sw-muted">
        Anteprima a campioni restituiti dal server (massimo 2000 per serie). Nessuna interpolazione,
        nessuna sostituzione con zero; le scale verticali sono indipendenti.
      </p>
    </>
  );
}

export function ChronoscopePage() {
  return (
    <>
      <header className="sw-head">
        <p className="sw-kicker">02 / Tempo e segnali</p>
        <h1>Cronoscopio</h1>
        <p>Una sola scala temporale, più tracce indipendenti. Esplora il supporto realmente acquisito.</p>
      </header>
      <WorkoutPicker>
        {(workout) => <Timeline key={workout.id + ":" + workout.revision} workout={workout} />}
      </WorkoutPicker>
    </>
  );
}
