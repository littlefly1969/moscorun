import type { SubmitEvent } from "react";
import { useMutation } from "@tanstack/react-query";
import { api, unwrap, type Schemas } from "../../api/client";

const number = new Intl.NumberFormat("it-IT", { maximumFractionDigits: 2 });
const percent = new Intl.NumberFormat("it-IT", { style: "percent", maximumFractionDigits: 1 });
const format = (value: number | null | undefined) => (value == null ? "—" : number.format(value));
const metrics = ["airTemperature", "humidity", "windSpeed", "windDirection"];
const fields = [
  ["threshold", "Soglia descrittiva temperatura (°C)", -40, 60, 25],
  ["weatherGap", "Gap massimo meteo (s)", 1, 3600, 900],
  ["gpsGap", "Gap massimo GPS (s)", 1, 60, 30],
  ["accuracy", "Errore orizzontale massimo (m)", 0.001, 100, 15],
  ["distance", "Spostamento orizzontale minimo (m)", 0.001, 500, 20],
] as const;

function Results({ report }: { report: Schemas["WorkoutEnvironmentReport"] }) {
  const { terrain, heatExposure, metrics: rows } = report.analysis;
  return (
    <>
      <p>Stato meteo: {report.weatherStatus}</p>
      {report.truncated && <p>Elenco limitato alle prime 64 revisioni.</p>}
      <dl>
        <dt>Tempo GPS con altimetria supportata (s)</dt>
        <dd>{format(terrain.supportedSeconds)}</dd>
        <dt>Distanza orizzontale supportata (m)</dt>
        <dd>{format(terrain.horizontalDistanceMeters)}</dd>
        <dt>Pendenza netta (%)</dt>
        <dd>{format(terrain.netGradePercent)}</dd>
        <dt>Dislivello positivo grezzo (m)</dt>
        <dd>{format(terrain.rawAscentMeters)}</dd>
        <dt>Dislivello negativo grezzo (m)</dt>
        <dd>{format(terrain.rawDescentMeters)}</dd>
        <dt>Minuti sopra soglia</dt>
        <dd>{format(heatExposure?.aboveThresholdMinutes)}</dd>
        <dt>Gradi-minuto sopra soglia (°C min)</dt>
        <dd>{format(heatExposure?.degreeMinutes)}</dd>
      </dl>
      <div className="table-scroll">
        <table>
          <thead>
            <tr>
              <th scope="col">Metrica</th>
              <th scope="col">Unità</th>
              <th scope="col">Copertura</th>
              <th scope="col">Media ponderata</th>
            </tr>
          </thead>
          <tbody>
            {rows.map((row) => (
              <tr key={row.metric}>
                <th scope="row">{row.metric}</th>
                <td>{row.unit}</td>
                <td>{percent.format(row.coverage)}</td>
                <td>{format(row.weightedMean)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </>
  );
}

export function WorkoutEnvironmentPanel({ workoutId }: { workoutId: string }) {
  const analysis = useMutation({
    mutationFn: (body: Schemas["WorkoutEnvironmentRequest"]) => {
      const params = { path: { id: workoutId } };
      return unwrap(api.POST("/api/v1/workouts/{id}/science/environment", { params, body }));
    },
  });
  const submit = (event: SubmitEvent<HTMLFormElement>) => {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    const source = form.get("source");
    if (typeof source !== "string") return;
    const chunkIds = metrics
      .map((metric) => form.get(metric))
      .filter((value): value is string => typeof value === "string" && value !== "");
    analysis.mutate({
      source,
      chunkIds,
      protocol: {
        heatThresholdCelsius: Number(form.get("threshold")),
        maximumWeatherGapSeconds: Number(form.get("weatherGap")),
        maximumGpsGapSeconds: Number(form.get("gpsGap")),
        maximumHorizontalAccuracyMeters: Number(form.get("accuracy")),
        minimumHorizontalDistanceMeters: Number(form.get("distance")),
        weatherRepresentative: form.has("representative"),
        windDirectionIsFrom: form.has("direction"),
      },
    });
  };
  const report = analysis.data;
  return (
    <section className="card" aria-labelledby="environment-title">
      <h2 id="environment-title">Contesto ambientale della corsa</h2>
      <p>GPS grezzo e meteo persistito, senza correzione automatica del passo o valutazione di sicurezza.</p>
      <p>Se non hai dati meteo selezionabili, puoi comunque analizzare il terreno.</p>
      <form className="inline-form" onSubmit={submit}>
        <label>
          Fonte meteo
          <input name="source" required maxLength={200} />
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
        {fields.map(([name, label, min, max, initial]) => (
          <label key={name}>
            {label}
            <input name={name} type="number" min={min} max={max} step="any" defaultValue={initial} required />
          </label>
        ))}
        <label>
          <input name="representative" type="checkbox" />
          Confermo meteo rappresentativo del percorso
        </label>
        <label>
          <input name="direction" type="checkbox" />
          Direzione del vento da cui proviene (gradi)
        </label>
        <button type="submit" disabled={analysis.isPending}>
          Leggi o analizza ambiente
        </button>
      </form>
      {analysis.isPending && <p role="status">Analisi in corso…</p>}
      {analysis.isError && <p role="alert">Analisi ambientale non disponibile.</p>}
      {report && <Results report={report} />}
    </section>
  );
}
