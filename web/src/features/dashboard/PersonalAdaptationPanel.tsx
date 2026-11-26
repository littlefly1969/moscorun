import { useMutation } from "@tanstack/react-query";
import type { SubmitEvent } from "react";
import { api, unwrap, type Schemas } from "../../api/client";

interface Protocol {
  source: string;
  from: string;
  split: string;
  until: string;
  distanceMeters: number;
}

const number = new Intl.NumberFormat("it-IT", { maximumFractionDigits: 3 });
const format = (value: number | null) => (value == null ? "—" : number.format(value));

function Results({ report }: { report: Schemas["PersonalAdaptationReport"] }) {
  const complete = report.loads.filter((point) => point.load != null).length;
  return (
    <>
      <p>Stato: {report.status}</p>
      <p>
        Giorni con carico attestato: {complete}/{report.loads.length}
      </p>
      <dl>
        <dt>Prove di calibrazione</dt>
        <dd>{report.model?.trainingCount ?? 0}</dd>
        <dt>Costante fitness (giorni)</dt>
        <dd>{format(report.model?.parameters.fitnessTimeConstantDays ?? null)}</dd>
        <dt>Costante fatigue (giorni)</dt>
        <dd>{format(report.model?.parameters.fatigueTimeConstantDays ?? null)}</dd>
        <dt>MAE holdout (m/s)</dt>
        <dd>{format(report.holdout?.meanAbsoluteError ?? null)}</dd>
        <dt>MAE baseline (m/s)</dt>
        <dd>{format(report.baselineHoldout?.meanAbsoluteError ?? null)}</dd>
        <dt>Velocità prevista oggi (m/s)</dt>
        <dd>{format(report.currentPredictedSpeedMetersPerSecond)}</dd>
      </dl>
      <p>Stati iniziali nulli per ipotesi; confronto retrospettivo, non prova causale.</p>
      <div className="table-scroll">
        <table>
          <thead>
            <tr>
              <th scope="col">Prova UTC</th>
              <th scope="col">Ruolo</th>
              <th scope="col">Osservato (m/s)</th>
              <th scope="col">Previsto (m/s)</th>
            </tr>
          </thead>
          <tbody>
            {report.tests.map((test) => (
              <tr key={test.workoutId}>
                <th scope="row">{test.startedAt}</th>
                <td>{test.role}</td>
                <td>{format(test.observedSpeedMetersPerSecond)}</td>
                <td>{format(test.predictedSpeedMetersPerSecond)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </>
  );
}

export function PersonalAdaptationPanel() {
  const analysis = useMutation({
    mutationFn: (query: Protocol) => unwrap(api.GET("/api/v1/science/adaptation", { params: { query } })),
  });
  const submit = (event: SubmitEvent<HTMLFormElement>) => {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    const source = form.get("source");
    const from = form.get("from");
    const split = form.get("split");
    const until = form.get("until");
    if (typeof source !== "string" || typeof from !== "string") return;
    if (typeof split !== "string" || typeof until !== "string") return;
    analysis.mutate({ source, from, split, until, distanceMeters: Number(form.get("distance")) });
  };
  return (
    <section className="card" aria-labelledby="adaptation-title">
      <h2 id="adaptation-title">Adattamento fitness-fatigue</h2>
      <p>Carico session-RPE giornaliero attestato e prove massimali sulla stessa distanza.</p>
      <p>Finestra fino a 180 giorni. Il giorno finale è escluso.</p>
      <form className="inline-form" onSubmit={submit}>
        <label>
          Sorgente prove
          <input name="source" required maxLength={32} />
        </label>
        <label>
          Inizio storico UTC
          <input name="from" type="date" required />
        </label>
        <label>
          Cutoff addestramento UTC
          <input name="split" type="date" required />
        </label>
        <label>
          Fine holdout esclusa UTC
          <input name="until" type="date" required />
        </label>
        <label>
          Distanza delle prove (m)
          <input name="distance" type="number" min={100} max={100000} required />
        </label>
        <button type="submit" disabled={analysis.isPending}>
          Calibra adattamento
        </button>
      </form>
      {analysis.isPending && <p role="status">Analisi in corso…</p>}
      {analysis.isError && <p role="alert">Calibrazione non riuscita.</p>}
      {analysis.data && <Results report={analysis.data} />}
    </section>
  );
}
