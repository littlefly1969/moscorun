import type { SubmitEvent } from "react";
import { useMutation } from "@tanstack/react-query";
import { api, unwrap, type Schemas } from "../../api/client";

const number = new Intl.NumberFormat("it-IT", { maximumFractionDigits: 2 });
const format = (value: number | null) => (value == null ? "—" : number.format(value));

function Results({ report }: { report: Schemas["WorkoutReserveReport"] }) {
  return (
    <>
      <p>Stato: {report.status}</p>
      <p>
        Supporto continuo: {number.format(report.supportedSeconds)} /{" "}
        {number.format(report.workoutDurationSeconds)} s
      </p>
      <dl>
        <dt>Riserva minima ({report.unit})</dt>
        <dd>{format(report.minimumRemaining)}</dd>
        <dt>Riserva finale valutata ({report.unit})</dt>
        <dd>{format(report.lastRemaining)}</dd>
        <dt>Esaurimento condizionale (s)</dt>
        <dd>{format(report.exhaustionSeconds)}</dd>
      </dl>
      <div className="table-scroll">
        <table>
          <thead>
            <tr>
              <th scope="col">Tempo (s)</th>
              <th scope="col">Riserva ({report.unit})</th>
            </tr>
          </thead>
          <tbody>
            {report.points.map((point) => (
              <tr key={point.cumulativeSeconds}>
                <th scope="row">{number.format(point.cumulativeSeconds)}</th>
                <td>{number.format(point.remaining)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </>
  );
}

export function WorkoutReservePanel({ workoutId }: { workoutId: string }) {
  const analysis = useMutation({
    mutationFn: (query: Schemas["WorkoutReserveProtocol"]) => {
      const params = { path: { id: workoutId }, query };
      return unwrap(api.GET("/api/v1/workouts/{id}/science/reserve", { params }));
    },
  });
  const submit = (event: SubmitEvent<HTMLFormElement>) => {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    const metric = form.get("metric");
    if (metric !== "Speed" && metric !== "Power") return;
    analysis.mutate({
      metric,
      criticalIntensity: Number(form.get("critical")),
      capacity: Number(form.get("capacity")),
      initialRemaining: Number(form.get("initial")),
      recoveryTimeConstantSeconds: Number(form.get("tau")),
    });
  };

  return (
    <section className="card" aria-labelledby="reserve-title">
      <h2 id="reserve-title">Replay riserva critica D′ / W′</h2>
      <p>Simulazione condizionale su dati continui. Si interrompe alla prima lacuna o all'esaurimento.</p>
      <form className="inline-form" onSubmit={submit}>
        <label>
          Canale
          <select name="metric">
            <option value="Speed">Velocità (m/s), riserva (m)</option>
            <option value="Power">Potenza (W), riserva (J)</option>
          </select>
        </label>
        <label>
          Intensità critica calibrata
          <input name="critical" type="number" min="0.000001" step="any" required />
        </label>
        <label>
          Capacità calibrata
          <input name="capacity" type="number" min="0.000001" step="any" required />
        </label>
        <label>
          Riserva iniziale ipotizzata
          <input name="initial" type="number" min="0.000001" step="any" required />
        </label>
        <label>
          Costante di recupero calibrata (s)
          <input name="tau" type="number" min="0.000001" step="any" required />
        </label>
        <button type="submit" disabled={analysis.isPending}>
          Simula riserva
        </button>
      </form>
      {analysis.isPending && <p role="status">Analisi in corso…</p>}
      {analysis.isError && <p role="alert">Replay non riuscito. Verifica i parametri.</p>}
      {analysis.data && <Results report={analysis.data} />}
    </section>
  );
}
