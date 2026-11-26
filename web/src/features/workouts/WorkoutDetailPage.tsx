import { lazy, Suspense } from "react";
import { Link, useParams } from "react-router";
import { ApiError } from "../../api/client";
import {
  activityLabel,
  formatBpm,
  formatDateTime,
  formatDistance,
  formatDuration,
  formatPace,
} from "../../lib/format";
import { StatCard } from "../dashboard/StatCard";
import { RouteMap } from "./RouteMap";
import { useWorkout } from "./queries";
import { WorkoutSciencePanel } from "./WorkoutSciencePanel";

const HeartRateChart = lazy(() => import("./HeartRateChart").then((m) => ({ default: m.HeartRateChart })));

export function WorkoutDetailPage() {
  const { id = "" } = useParams();
  const workout = useWorkout(id);

  if (workout.isPending) return <p role="status">Caricamento…</p>;
  if (workout.isError) {
    return (
      <p role="alert">
        {workout.error instanceof ApiError && workout.error.status === 404
          ? "Allenamento non trovato."
          : "Impossibile caricare l'allenamento."}{" "}
        <Link to="/allenamenti">Torna agli allenamenti</Link>
      </p>
    );
  }

  const w = workout.data.summary;
  const seconds = w.activeDurationSeconds ?? (Date.parse(w.endedAt) - Date.parse(w.startedAt)) / 1000;
  const hasHeartRate = workout.data.series.some((s) => s.metric === "HeartRate");

  return (
    <>
      <p>
        <Link to="/allenamenti">← Allenamenti</Link>
      </p>
      <h1>
        {activityLabel(w.activityType)} · {formatDateTime(w.startedAt)}
      </h1>
      <div className="stats">
        <StatCard label="Distanza" value={formatDistance(w.distanceM)} />
        <StatCard
          label="Durata"
          value={formatDuration(seconds)}
          hint={w.activeDurationSeconds != null ? "senza pause" : "totale"}
        />
        <StatCard label="Passo medio" value={formatPace(seconds, w.distanceM)} />
        <StatCard
          label="FC media"
          value={formatBpm(w.averageHeartRate)}
          hint={w.maxHeartRate != null ? `max ${formatBpm(w.maxHeartRate)}` : undefined}
        />
      </div>

      <section className="card" aria-labelledby="route-title">
        <h2 id="route-title">Percorso</h2>
        <RouteMap workoutId={w.id} hasTrack={w.trackPointCount > 0} />
      </section>

      <WorkoutSciencePanel workoutId={w.id} revision={w.revision} />

      {hasHeartRate && (
        <section className="card" aria-labelledby="hr-title">
          <h2 id="hr-title">Frequenza cardiaca</h2>
          <Suspense fallback={<p role="status">Caricamento del grafico…</p>}>
            <HeartRateChart workoutId={w.id} startedAt={w.startedAt} />
          </Suspense>
        </section>
      )}
    </>
  );
}
