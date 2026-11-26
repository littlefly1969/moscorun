import type { SubmitEvent } from "react";
import { useMutation } from "@tanstack/react-query";
import { api, unwrap, type Schemas } from "../../api/client";

interface Protocol {
  source: string;
  last: string;
  days: number;
  lag: number;
  distanceMeters: number;
}

const number = new Intl.NumberFormat("it-IT", { maximumFractionDigits: 3 });
const format = (value: number | null) => (value == null ? "—" : number.format(value));

function Results({ report }: { report: Schemas["SleepPerformanceReport"] }) {
  return (
    <>
      <p>Stato: {report.status}</p>
      <p>
        Prove accoppiate: {report.pairedTests}/{report.evidence.length}
      </p>
      <dl>
        <dt>Pearson r durata sonno / velocità</dt>
        <dd>{format(report.sleepHoursCorrelation)}</dd>
        <dt>Pearson r efficienza sonno / velocità</dt>
        <dd>{format(report.sleepEfficiencyCorrelation)}</dd>
      </dl>
      <p>Associazione descrittiva non aggiustata per carico, meteo o altri confondenti.</p>
      <div className="table-scroll">
        <table>
          <thead>
            <tr>
              <th scope="col">Prova UTC</th>
              <th scope="col">Notte UTC</th>
              <th scope="col">Stato</th>
              <th scope="col">Sonno (h)</th>
              <th scope="col">Velocità (m/s)</th>
            </tr>
          </thead>
          <tbody>
            {report.evidence.map((item) => (
              <tr key={item.workoutId}>
                <th scope="row">{item.testStartedAt}</th>
                <td>{item.sleepDay}</td>
                <td>{item.status}</td>
                <td>{format(item.sleepHours)}</td>
                <td>{format(item.speedMetersPerSecond)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </>
  );
}

export function SleepPerformancePanel() {
  const analysis = useMutation({
    mutationFn: (query: Protocol) =>
      unwrap(api.GET("/api/v1/science/sleep-performance", { params: { query } })),
  });
  const submit = (event: SubmitEvent<HTMLFormElement>) => {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    const source = form.get("source");
    const last = form.get("last");
    if (typeof source !== "string" || typeof last !== "string") return;
    analysis.mutate({
      source,
      last,
      days: Number(form.get("days")),
      lag: Number(form.get("lag")),
      distanceMeters: Number(form.get("distance")),
    });
  };
  return (
    <section className="card" aria-labelledby="sleep-performance-title">
      <h2 id="sleep-performance-title">Sonno e prestazione</h2>
      <p>Prove massimali sulla stessa distanza, con notti dichiarate prima della prova.</p>
      <p>Lag di calendario UTC; lag 1 indica la notte conclusa nel giorno precedente.</p>
      <form className="inline-form" onSubmit={submit}>
        <label>
          Sorgente prove sonno
          <input name="source" required maxLength={32} />
        </label>
        <label>
          Ultimo giorno prove UTC
          <input name="last" type="date" required />
        </label>
        <label>
          Finestra prove (giorni)
          <input name="days" type="number" min={7} max={90} defaultValue={28} required />
        </label>
        <label>
          Lag notte (giorni UTC)
          <input name="lag" type="number" min={1} max={7} defaultValue={1} required />
        </label>
        <label>
          Distanza prove sonno (m)
          <input name="distance" type="number" min={100} max={100000} required />
        </label>
        <button type="submit" disabled={analysis.isPending}>
          Confronta sonno e prestazione
        </button>
      </form>
      {analysis.isPending && <p role="status">Analisi in corso…</p>}
      {analysis.isError && <p role="alert">Confronto non disponibile.</p>}
      {analysis.data && <Results report={analysis.data} />}
    </section>
  );
}
