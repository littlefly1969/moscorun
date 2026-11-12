import { CartesianGrid, Line, LineChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";
import type { Weighing } from "../../api/client";
import { toChartPoints } from "./chartPoints";
import { formatKg, formatShortDate } from "../../lib/format";

export function WeightChart({ items }: { items: readonly Weighing[] }) {
  const points = toChartPoints(items);
  if (points.length < 2) {
    return <p className="muted">Servono almeno due pesate per disegnare l&apos;andamento.</p>;
  }
  return (
    <figure className="chart" aria-label="Andamento del peso">
      <ResponsiveContainer width="100%" height={260}>
        <LineChart data={points} margin={{ top: 8, right: 16, bottom: 0, left: 0 }}>
          <CartesianGrid strokeDasharray="3 3" stroke="var(--border)" />
          <XAxis
            dataKey="t"
            type="number"
            scale="time"
            domain={["dataMin", "dataMax"]}
            tickFormatter={formatShortDate}
            stroke="var(--muted)"
          />
          <YAxis
            domain={[(min: number) => Math.floor(min - 0.5), (max: number) => Math.ceil(max + 0.5)]}
            allowDecimals={false}
            width={40}
            stroke="var(--muted)"
          />
          <Tooltip
            labelFormatter={(t) => formatShortDate(Number(t))}
            formatter={(v) => [formatKg(Number(v)), "Peso"]}
          />
          <Line type="monotone" dataKey="kg" stroke="var(--accent)" strokeWidth={2} dot={{ r: 3 }} />
        </LineChart>
      </ResponsiveContainer>
    </figure>
  );
}
