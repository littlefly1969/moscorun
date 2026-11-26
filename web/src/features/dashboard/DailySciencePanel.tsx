import { useMutation } from "@tanstack/react-query";
import { useState, type SubmitEvent } from "react";
import { api, unwrap, type Schemas } from "../../api/client";
import { ScienceSourceField } from "../science/ScienceSourceField";

const labels = {
  restingHeartRate: "Frequenza cardiaca a riposo",
  hrvRmssd: "HRV RMSSD",
  hrvSdnn: "HRV SDNN",
  sleepDuration: "Durata sonno",
  sleepEfficiency: "Efficienza sonno",
  weight: "Peso",
  fatPercentage: "Percentuale di grasso",
  fatMass: "Massa grassa",
  fatFreeMass: "Massa magra",
  fatigue: "Fatica",
  soreness: "Indolenzimento",
  sleepQuality: "Qualità del sonno",
  stressPerceived: "Stress percepito",
  motivation: "Motivazione",
} as const;

const statuses: Record<string, string> = {
  observed: "Osservato",
  missing: "Non disponibile",
  ambiguous: "Misure o revisioni ambigue",
  invalid: "Valore non valido",
  "incompatible-method": "Metodo non comparabile",
  "insufficient-baseline": "Baseline insufficiente",
};

const number = new Intl.NumberFormat("it-IT", { maximumFractionDigits: 2 });
const format = (value: number | null, unit = "") =>
  value == null ? "—" : [number.format(value), unit].filter(Boolean).join(" ");

function Report({ report }: { report: Schemas["DailyMetricReport"] }) {
  const baseline = report.baseline;
  return (
    <>
      <p>
        {statuses[report.status] ?? report.status} · {report.source} · {report.current.day} UTC
      </p>
      <p className="muted">
        {report.methodVersion} · dati disponibili al {report.asOf} ·{" "}
        {report.current.method ?? "metodo assente"}
      </p>
      <dl>
        <dt>Valore giornaliero</dt>
        <dd>{format(report.current.value, report.unit)}</dd>
        <dt>Mediana personale</dt>
        <dd>{format(baseline?.baselineMedian ?? null, report.unit)}</dd>
        <dt>Giorni validi nella baseline</dt>
        <dd>
          {report.current.status === "observed"
            ? report.history.filter((point) => point.status === "observed").length
            : "—"}
        </dd>
        <dt>Deviazione robusta (z)</dt>
        <dd>{format(baseline?.robustZScore ?? null)}</dd>
        <dt>Variazione relativa alla mediana</dt>
        <dd>{format(report.currentVersusMedianPercent, "%")}</dd>
        <dt>Trend robusto del peso</dt>
        <dd>{format(report.weightTrendKgPerWeek, "kg/settimana")}</dd>
      </dl>
      <div className="table-scroll">
        <table>
          <thead>
            <tr>
              <th scope="col">Giorno UTC</th>
              <th scope="col">Valore</th>
              <th scope="col">Qualità</th>
            </tr>
          </thead>
          <tbody>
            {report.history.map((point) => (
              <tr key={point.day}>
                <th scope="row">{point.day}</th>
                <td>{format(point.value, report.unit)}</td>
                <td>{statuses[point.status] ?? point.status}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </>
  );
}

export function DailySciencePanel() {
  const analysis = useMutation({
    mutationFn: (query: { metric: string; source: string; day: string; baselineDays: number }) =>
      unwrap(api.GET("/api/v1/science/daily", { params: { query } })),
  });
  const [yesterday] = useState(() => new Date(Date.now() - 86400000).toISOString().slice(0, 10));
  const submit = (event: SubmitEvent<HTMLFormElement>) => {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    const metric = form.get("metric");
    const source = form.get("source");
    const day = form.get("day");
    const baseline = form.get("baseline");
    if (typeof metric !== "string" || typeof source !== "string" || typeof day !== "string") return;
    analysis.mutate({ metric, source: source.trim(), day, baselineDays: Number(baseline) });
  };

  return (
    <section className="card" aria-labelledby="daily-science-title">
      <h2 id="daily-science-title">Recupero e composizione corporea</h2>
      <p>
        Confronta osservazioni giornaliere della stessa sorgente e dello stesso metodo. Nessun punteggio
        clinico; i giorni mancanti o ambigui non vengono imputati.
      </p>
      <form className="inline-form" onSubmit={submit}>
        <label>
          Metrica giornaliera
          <select name="metric" defaultValue="restingHeartRate">
            {Object.entries(labels).map(([key, label]) => (
              <option key={key} value={key}>
                {label}
              </option>
            ))}
          </select>
        </label>
        <ScienceSourceField label="Sorgente scientifica" />
        <label>
          Giorno UTC
          <input name="day" type="date" max={yesterday} defaultValue={yesterday} required />
        </label>
        <label>
          Giorni di baseline
          <input name="baseline" type="number" min={7} max={90} defaultValue={28} required />
        </label>
        <button type="submit" disabled={analysis.isPending}>
          Analizza recupero e forma
        </button>
      </form>
      {analysis.isPending && <p role="status">Analisi in corso…</p>}
      {analysis.isError && <p role="alert">Analisi non riuscita: verifica sorgente, metrica e finestra.</p>}
      {analysis.data && <Report report={analysis.data} />}
    </section>
  );
}
