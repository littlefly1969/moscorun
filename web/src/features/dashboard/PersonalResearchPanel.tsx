import type { SubmitEvent } from "react";
import { useMutation } from "@tanstack/react-query";
import { api, unwrap, type Schemas } from "../../api/client";

interface Protocol {
  source: string;
  from: string;
  split: string;
  until: string;
  distanceMeters: number;
  penalty: number;
}

const number = new Intl.NumberFormat("it-IT", { maximumFractionDigits: 3 });
const format = (value: number | null | undefined) => (value == null ? "—" : number.format(value));

function Results({ report }: { report: Schemas["PersonalResearchReport"] }) {
  return (
    <>
      <p>Stato: {report.status}</p>
      <p>
        Prove con caratteristiche disponibili: {report.examples.length}/{report.candidateTests}
      </p>
      <p>MAE in m/s, stessa popolazione di test per modello completo, ablation e baseline.</p>
      <div className="table-scroll">
        <table>
          <thead>
            <tr>
              <th scope="col">Cutoff UTC</th>
              <th scope="col">Stato</th>
              <th scope="col">Training</th>
              <th scope="col">Test</th>
              <th scope="col">MAE completo</th>
              <th scope="col">MAE senza carico</th>
              <th scope="col">MAE media training</th>
            </tr>
          </thead>
          <tbody>
            {report.folds.map((fold) => (
              <tr key={fold.fold.cutoff}>
                <th scope="row">{fold.fold.cutoff}</th>
                <td>{fold.status}</td>
                <td>{fold.trainingCount}</td>
                <td>{fold.testCount}</td>
                <td>{format(fold.full?.model.meanAbsoluteError)}</td>
                <td>{format(fold.ablated?.model.meanAbsoluteError)}</td>
                <td>{format(fold.full?.baseline.meanAbsoluteError)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </>
  );
}

export function PersonalResearchPanel() {
  const analysis = useMutation({
    mutationFn: (query: Protocol) => unwrap(api.GET("/api/v1/science/research", { params: { query } })),
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
    analysis.mutate({
      source,
      from,
      split,
      until,
      distanceMeters: Number(form.get("distance")),
      penalty: Number(form.get("penalty")),
    });
  };
  return (
    <section className="card" aria-labelledby="research-title">
      <h2 id="research-title">Research Lab N-of-1</h2>
      <p>Due fold temporali disgiunti, ridge con e senza carico, confronto con media del training.</p>
      <p>Penalità dichiarata prima del confronto. Non è validazione prospettica indipendente.</p>
      <form className="inline-form" onSubmit={submit}>
        <label>
          Sorgente prove ricerca
          <input name="source" required maxLength={32} />
        </label>
        <label>
          Inizio storico ricerca UTC
          <input name="from" type="date" required />
        </label>
        <label>
          Cutoff ricerca UTC
          <input name="split" type="date" required />
        </label>
        <label>
          Fine ricerca esclusa UTC
          <input name="until" type="date" required />
        </label>
        <label>
          Distanza prove ricerca (m)
          <input name="distance" type="number" min={100} max={100000} required />
        </label>
        <label>
          Penalità L2
          <input name="penalty" type="number" min="0.0001" max={1000} step="any" required />
        </label>
        <button type="submit" disabled={analysis.isPending}>
          Esegui benchmark
        </button>
      </form>
      {analysis.isPending && <p role="status">Analisi in corso…</p>}
      {analysis.isError && <p role="alert">Benchmark non disponibile.</p>}
      {analysis.data && <Results report={analysis.data} />}
    </section>
  );
}
