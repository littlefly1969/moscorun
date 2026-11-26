import { useMutation } from "@tanstack/react-query";
import type { SubmitEvent } from "react";
import { api, unwrap, type Schemas } from "../../api/client";

interface Protocol {
  source: string;
  from: string;
  split: string;
  until: string;
  targetMeters: number;
}

const number = new Intl.NumberFormat("it-IT", { maximumFractionDigits: 2 });
const format = (value: number | null) => (value == null ? "—" : number.format(value));

function Report({ report }: { report: Schemas["AttestedRaceReport"] }) {
  return (
    <>
      <p className="muted">{report.methodVersion} · {report.protocol}</p>
      <dl>
        <dt>Tempo previsto (s)</dt>
        <dd>{format(report.prediction?.predictedSeconds ?? null)}</dd>
        <dt>Limite inferiore empirico (s)</dt>
        <dd>{format(report.interval?.lowerSeconds ?? null)}</dd>
        <dt>Limite superiore empirico (s)</dt>
        <dd>{format(report.interval?.upperSeconds ?? null)}</dd>
        <dt>Esponente personale</dt>
        <dd>{format(report.model?.exponent ?? null)}</dd>
        <dt>Prove di fit</dt>
        <dd>{report.model?.trainingCount ?? 0}</dd>
      </dl>
      {!report.model && <p>Prove massimali disponibili al cutoff insufficienti.</p>}
      {report.model && !report.prediction && <p>Distanza fuori dal supporto del modello.</p>}
      {report.prediction && !report.interval && <p>Calibrazione temporale insufficiente per un intervallo.</p>}
      {report.interval && <p>Intervallo empirico nominale 80%, copertura non garantita.</p>}
      <div className="table-scroll">
        <table>
          <thead>
            <tr>
              <th scope="col">Fine prova UTC</th>
              <th scope="col">Ruolo</th>
              <th scope="col">Distanza (m)</th>
              <th scope="col">Tempo (s)</th>
              <th scope="col">Errore assoluto (s)</th>
            </tr>
          </thead>
          <tbody>
            {report.evidence.map((item) => (
              <tr key={item.workoutId}>
                <th scope="row">{item.endedAt}</th>
                <td>{item.role}</td>
                <td>{number.format(item.distanceMeters)}</td>
                <td>{number.format(item.durationSeconds)}</td>
                <td>{format(item.absoluteErrorSeconds)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </>
  );
}

export function AttestedRacePanel() {
  const analysis = useMutation({
    mutationFn: (query: Protocol) => unwrap(api.GET("/api/v1/science/race-calibration", { params: { query } })),
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
    analysis.mutate({ source, from, split, until, targetMeters: Number(form.get("target")) });
  };
  return (
    <section className="card" aria-labelledby="race-calibration-title">
      <h2 id="race-calibration-title">Calibrazione prestazionale personale</h2>
      <p>Usa prove massimali attestate su percorso piano misurato, senza pause e della stessa sorgente.</p>
      <p>Il cutoff UTC separa fit e calibrazione. L'ultimo giorno indicato è escluso.</p>
      <form className="inline-form" onSubmit={submit}>
        <label>
          Sorgente
          <input name="source" defaultValue="samsung-health" required maxLength={32} />
        </label>
        <label>
          Inizio fit UTC
          <input name="from" type="date" required />
        </label>
        <label>
          Cutoff fit UTC
          <input name="split" type="date" required />
        </label>
        <label>
          Fine calibrazione esclusa UTC
          <input name="until" type="date" required />
        </label>
        <label>
          Distanza obiettivo (m)
          <input name="target" type="number" min={100} max={100000} required />
        </label>
        <button type="submit" disabled={analysis.isPending}>
          Calibra prestazione
        </button>
      </form>
      {analysis.isPending && <p role="status">Analisi in corso…</p>}
      {analysis.isError && <p role="alert">Calibrazione non riuscita. Verifica il protocollo.</p>}
      {analysis.data && <Report report={analysis.data} />}
    </section>
  );
}
