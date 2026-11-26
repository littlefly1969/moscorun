import { useQueries } from "@tanstack/react-query";
import { useState, type SubmitEvent } from "react";
import { api, unwrap, type Schemas } from "../../api/client";
import { metricCatalog, type MetricDefinition } from "./metricCatalog";

const dayMs = 86400000;
const daily = metricCatalog.filter((metric) => metric.access === "daily");
const number = new Intl.NumberFormat("it-IT", { maximumFractionDigits: 2 });

interface Protocol {
  source: string;
  day: string;
  baselineDays: number;
}

interface Context {
  protocol: Protocol;
  metrics: MetricDefinition[];
}

function DayTrace({
  metric,
  report,
  start,
  end,
  cursor,
}: {
  metric: MetricDefinition;
  report: Schemas["DailyMetricReport"];
  start: number;
  end: number;
  cursor: number;
}) {
  const points = [...report.history, report.current].filter(
    (point) =>
      point.status === "observed" &&
      point.value != null &&
      Number.isFinite(point.value) &&
      Date.parse(point.day) >= start &&
      Date.parse(point.day) <= end,
  );
  const values = points.flatMap((point) => (point.value == null ? [] : [point.value]));
  const low = values.length > 0 ? Math.min(...values) : 0;
  const high = values.length > 0 ? Math.max(...values) : 0;
  const selected = points.find((point) => Date.parse(point.day) === cursor);
  const x = (time: number) => 10 + (780 * (time - start)) / Math.max(dayMs, end - start);
  const y = (value: number) => (high === low ? 42 : 70 - (56 * (value - low)) / (high - low));
  return (
    <section className="sw-trace" aria-label={metric.label}>
      <div className="sw-trace-label">
        <h2>{metric.label}</h2>
        <small>{report.unit}</small>
        <strong>{selected?.value == null ? "—" : number.format(selected.value)}</strong>
        <small>{selected ? (selected.method ?? "Metodo non indicato") : "Non osservato nel giorno"}</small>
        <small>{report.methodVersion}</small>
      </div>
      <figure className="sw-figure">
        <svg viewBox="0 0 800 84" role="img" aria-label={metric.label + " per giorno UTC"}>
          {[0, 1, 2, 3, 4, 5, 6, 7].map((i) => (
            <line
              key={i}
              x1={10 + (780 * i) / 7}
              x2={10 + (780 * i) / 7}
              y1="8"
              y2="76"
              stroke="currentColor"
              opacity=".12"
            />
          ))}
          {points.map((point) =>
            point.value == null ? null : (
              <circle
                key={point.day}
                cx={x(Date.parse(point.day))}
                cy={y(point.value)}
                r="2.5"
                fill="currentColor"
              />
            ),
          )}
          <line x1={x(cursor)} x2={x(cursor)} y1="0" y2="84" stroke="var(--sw-cursor)" strokeWidth="2" />
        </svg>
        <figcaption>
          {points.length} giorni osservati · {report.source} · cutoff {report.asOf}
        </figcaption>
      </figure>
    </section>
  );
}

export function DailyTimeline() {
  const [selected, setSelected] = useState(["restingHeartRate", "hrvRmssd", "sleepDuration"]);
  const [context, setContext] = useState<Context | null>(null);
  const [offset, setOffset] = useState(0);
  const [yesterday] = useState(() => new Date(Date.now() - dayMs).toISOString().slice(0, 10));
  const queries = useQueries({
    queries: (context?.metrics ?? []).map((metric) => ({
      queryKey: ["workspace", "daily", metric.id, context?.protocol],
      queryFn: () => {
        if (!context) throw new Error("Contesto non selezionato");
        return unwrap(
          api.GET("/api/v1/science/daily", {
            params: { query: { metric: metric.id, ...context.protocol } },
          }),
        );
      },
    })),
  });
  const submit = (event: SubmitEvent<HTMLFormElement>) => {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    const source = form.get("source");
    const day = form.get("day");
    if (typeof source !== "string" || typeof day !== "string" || !source.trim()) return;
    const baselineDays = Number(form.get("baselineDays"));
    const metrics = daily.filter((metric) => selected.includes(metric.id));
    setContext({ protocol: { source: source.trim(), day, baselineDays }, metrics });
    setOffset(baselineDays);
  };
  const end = context ? Date.parse(context.protocol.day) : 0;
  const start = end - (context?.protocol.baselineDays ?? 0) * dayMs;
  const cursor = start + offset * dayMs;
  const date = (time: number) => new Date(time).toISOString().slice(0, 10);
  return (
    <>
      <form onSubmit={submit}>
        <div className="sw-controls">
          <label>
            Sorgente storica
            <input name="source" defaultValue="samsung-health" maxLength={200} required />
          </label>
          <label>
            Ultimo giorno UTC
            <input name="day" type="date" defaultValue={yesterday} max={yesterday} required />
          </label>
          <label>
            Giorni precedenti
            <input name="baselineDays" type="number" min={7} max={90} defaultValue={28} required />
          </label>
          <button type="submit" disabled={selected.length === 0}>
            Carica tracce giornaliere
          </button>
        </div>
        <fieldset className="sw-trace-options">
          <legend>Metriche da sincronizzare</legend>
          {daily.map((metric) => (
            <label className="sw-check" key={metric.id}>
              <input
                type="checkbox"
                checked={selected.includes(metric.id)}
                onChange={(event) => {
                  setSelected((items) =>
                    event.target.checked ? [...items, metric.id] : items.filter((id) => id !== metric.id),
                  );
                }}
              />
              {metric.label}
            </label>
          ))}
        </fieldset>
      </form>
      {context && (
        <>
          <div className="sw-controls">
            <label className="sw-scrubber">
              Giorno del cursore · {date(cursor)}
              <input
                type="range"
                min={0}
                max={context.protocol.baselineDays}
                value={offset}
                onChange={(event) => {
                  setOffset(Number(event.target.value));
                }}
              />
            </label>
          </div>
          <div className="sw-ruler">
            <span>{date(start)}</span>
            <span>{date(end)}</span>
          </div>
          {context.metrics.map((metric, index) => {
            const query = queries[index];
            if (query?.isPending)
              return (
                <p key={metric.id} role="status">
                  Caricamento di {metric.label}…
                </p>
              );
            if (query?.isError || !query?.data) {
              return (
                <p key={metric.id} role="alert">
                  Lettura non riuscita: {metric.label}.
                </p>
              );
            }
            return (
              <DayTrace
                key={metric.id}
                metric={metric}
                report={query.data}
                start={start}
                end={end}
                cursor={cursor}
              />
            );
          })}
          <p className="sw-muted">
            Giorni UTC esatti, scale verticali indipendenti. I punti mancanti non vengono interpolati.
          </p>
        </>
      )}
    </>
  );
}
