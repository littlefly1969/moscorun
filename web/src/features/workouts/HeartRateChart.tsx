import { CartesianGrid, Line, LineChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";
import { formatDuration } from "../../lib/format";
import { useSeries } from "./queries";

/** Frequenza cardiaca sul tempo trascorso dall'inizio; al più ~600 punti disegnati per restare fluido sul telefono. */
export function HeartRateChart({ workoutId, startedAt }: { workoutId: string; startedAt: string }) {
  const series = useSeries(workoutId);
  if (series.isPending) return <p role="status">Caricamento del grafico…</p>;
  if (series.isError) return <p className="muted">Serie non disponibile.</p>;
  const hr = series.data.items.find((s) => s.metric === "HeartRate");
  if (!hr || hr.times.length < 2) return <p className="muted">Campioni insufficienti.</p>;

  const start = Date.parse(startedAt);
  const step = Math.max(1, Math.ceil(hr.times.length / 600));
  const points = hr.times
    .map((t, i) => ({ s: (t - start) / 1000, bpm: hr.values[i] ?? 0 }))
    .filter((_, i) => i % step === 0);

  return (
    <figure className="chart" aria-label="Frequenza cardiaca durante l'allenamento">
      <ResponsiveContainer width="100%" height={220}>
        <LineChart data={points} margin={{ top: 8, right: 16, bottom: 0, left: 0 }}>
          <CartesianGrid strokeDasharray="3 3" stroke="var(--border)" />
          <XAxis
            dataKey="s"
            type="number"
            domain={["dataMin", "dataMax"]}
            tickFormatter={(v: number) => formatDuration(v)}
            stroke="var(--muted)"
          />
          <YAxis
            domain={["dataMin - 5", "dataMax + 5"]}
            allowDecimals={false}
            width={40}
            stroke="var(--muted)"
          />
          <Tooltip
            labelFormatter={(v) => formatDuration(Number(v))}
            formatter={(v) => [`${Math.round(Number(v))} bpm`, "FC"]}
          />
          <Line
            type="monotone"
            dataKey="bpm"
            stroke="var(--danger)"
            strokeWidth={1.5}
            dot={false}
            isAnimationActive={false}
          />
        </LineChart>
      </ResponsiveContainer>
    </figure>
  );
}
