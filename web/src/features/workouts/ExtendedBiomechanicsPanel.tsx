import type { SubmitEvent } from "react";
import { useMutation } from "@tanstack/react-query";
import { api, unwrap, type Schemas } from "../../api/client";

interface Protocol {
  source: string;
  protocolConfirmed: boolean;
  cadenceIsStepsPerMinute: boolean;
  balanceIsLeftPercent: boolean;
  chunkIds?: string;
}

const number = new Intl.NumberFormat("it-IT", { maximumFractionDigits: 3 });
const percent = new Intl.NumberFormat("it-IT", { style: "percent", maximumFractionDigits: 1 });
const format = (value: number | null) => (value == null ? "—" : number.format(value));
const metrics = ["speed", "cadence", "groundContactTime", "verticalOscillation", "groundContactBalance"];

function Results({ report }: { report: Schemas["ExtendedBiomechanicsReport"] }) {
  return (
    <>
      <p>Stato: {report.status}</p>
      <p>Revisioni selezionate: {report.selected.length}</p>
      {report.truncated && <p>Elenco limitato alle prime 64 revisioni.</p>}
      <div className="table-scroll">
        <table>
          <thead>
            <tr>
              <th scope="col">Metrica</th>
              <th scope="col">Unità</th>
              <th scope="col">Copertura</th>
              <th scope="col">Media ponderata</th>
              <th scope="col">CV (%)</th>
            </tr>
          </thead>
          <tbody>
            {report.metrics.map((metric) => (
              <tr key={metric.metric}>
                <th scope="row">{metric.metric}</th>
                <td>{metric.unit}</td>
                <td>{percent.format(metric.coverage)}</td>
                <td>{format(metric.weightedMean)}</td>
                <td>{format(metric.weightedCvPercent)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </>
  );
}

export function ExtendedBiomechanicsPanel({ workoutId }: { workoutId: string }) {
  const analysis = useMutation({
    mutationFn: (query: Protocol) => {
      const params = { path: { id: workoutId }, query };
      return unwrap(api.GET("/api/v1/workouts/{id}/science/biomechanics", { params }));
    },
  });
  const submit = (event: SubmitEvent<HTMLFormElement>) => {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    const source = form.get("source");
    if (typeof source !== "string") return;
    const ids = metrics.map((metric) => form.get(metric)).filter((value) => value != null && value !== "");
    const query: Protocol = {
      source,
      protocolConfirmed: form.has("protocol"),
      cadenceIsStepsPerMinute: form.has("cadenceUnit"),
      balanceIsLeftPercent: form.has("balanceUnit"),
    };
    if (ids.length > 0) query.chunkIds = ids.join(",");
    analysis.mutate(query);
  };
  const report = analysis.data;
  return (
    <section className="card" aria-labelledby="extended-biomechanics-title">
      <h2 id="extended-biomechanics-title">Dinamiche di corsa estese</h2>
      <p>Seleziona al più una revisione per canale, dello stesso dispositivo e protocollo.</p>
      <p>I risultati usano soltanto supporto temporale comune. Non sono misure di singoli passi.</p>
      <form className="inline-form" onSubmit={submit}>
        <label>
          Sorgente dinamiche
          <input name="source" maxLength={200} required />
        </label>
        {metrics.map((metric) => (
          <label key={metric}>
            {metric}
            <select name={metric}>
              <option value="">Non selezionato</option>
              {report?.chunks
                .filter((chunk) => chunk.metric === metric)
                .map((chunk) => (
                  <option key={chunk.id} value={chunk.id}>
                    {chunk.externalId} · {chunk.revision} · {chunk.firstSample}
                  </option>
                ))}
            </select>
          </label>
        ))}
        <label>
          <input name="protocol" type="checkbox" />
          Confermo stesso dispositivo, algoritmo e protocollo
        </label>
        <label>
          <input name="cadenceUnit" type="checkbox" />
          Cadenza in passi totali al minuto
        </label>
        <label>
          <input name="balanceUnit" type="checkbox" />
          Bilanciamento contatto come percentuale sinistra
        </label>
        <button type="submit" disabled={analysis.isPending}>
          Leggi o analizza dinamiche
        </button>
      </form>
      {analysis.isPending && <p role="status">Analisi in corso…</p>}
      {analysis.isError && <p role="alert">Dinamiche non disponibili.</p>}
      {report && <Results report={report} />}
    </section>
  );
}
