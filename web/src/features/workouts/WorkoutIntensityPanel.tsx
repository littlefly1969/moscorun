import type { SubmitEvent } from "react";
import { useMutation } from "@tanstack/react-query";
import { api, unwrap, type Schemas } from "../../api/client";

type Protocol = {
  metric: Schemas["SeriesMetric"];
  thresholds: string;
  restingBpm?: number;
  maximumBpm?: number;
  trimpA?: number;
  trimpB?: number;
};

const number = new Intl.NumberFormat("it-IT", { maximumFractionDigits: 2 });
const percent = new Intl.NumberFormat("it-IT", { style: "percent", maximumFractionDigits: 1 });
const format = (value: number | null) => (value == null ? "—" : number.format(value));

function Results({ report }: { report: Schemas["WorkoutIntensityReport"] }) {
  const d = report.distribution;
  return (
    <>
      <p className="muted">
        Revisione {report.revision} · {report.methodVersion} · gap massimo {report.maxGapSeconds} s.
      </p>
      <p>
        Copertura temporale: {percent.format(d.coverage)} · minuti senza supporto:{" "}
        {number.format(d.missingMinutes)}.
      </p>
      <dl>
        <dt>Media osservata ({report.unit})</dt>
        <dd>{format(report.observedMean)}</dd>
        <dt>Carico riserva cardiaca sul supporto osservato</dt>
        <dd>{format(report.supportedHeartRateReserveLoad)}</dd>
        <dt>TRIMP esponenziale sul supporto osservato</dt>
        <dd>{format(report.supportedExponentialTrimp)}</dd>
      </dl>
      {report.heartRateStatus === "outside-calibration" && (
        <p>Frequenza cardiaca fuori dall'intervallo di calibrazione: carico non calcolato.</p>
      )}
      <div className="table-scroll">
        <table>
          <thead>
            <tr>
              <th scope="col">Zona</th>
              <th scope="col">Soglia superiore ({report.unit}, esclusa)</th>
              <th scope="col">Minuti osservati</th>
            </tr>
          </thead>
          <tbody>
            {d.zoneMinutes.map((minutes, index) => (
              <tr key={index}>
                <th scope="row">{index + 1}</th>
                <td>{format(report.protocol.upperBoundaries[index] ?? null)}</td>
                <td>{number.format(minutes)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </>
  );
}

function optional(form: FormData, key: string) {
  const raw = form.get(key);
  return typeof raw === "string" && raw.trim() !== "" ? Number(raw) : undefined;
}

export function WorkoutIntensityPanel({ workoutId }: { workoutId: string }) {
  const analysis = useMutation({
    mutationFn: (query: Protocol) =>
      unwrap(api.GET("/api/v1/workouts/{id}/science/intensity", { params: { path: { id: workoutId }, query } })),
  });
  const submit = (event: SubmitEvent<HTMLFormElement>) => {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    const metric = form.get("metric");
    const thresholds = form.get("thresholds");
    if ((metric !== "HeartRate" && metric !== "Speed" && metric !== "Power") || typeof thresholds !== "string") {
      return;
    }
    const query: Protocol = { metric, thresholds };
    const fields = [
      ["restingBpm", "resting"],
      ["maximumBpm", "maximum"],
      ["trimpA", "a"],
      ["trimpB", "b"],
    ] as const;
    for (const [key, name] of fields) {
      const value = optional(form, name);
      if (value != null) query[key] = value;
    }
    analysis.mutate(query);
  };

  return (
    <section className="card" aria-labelledby="intensity-title">
      <h2 id="intensity-title">Zone d'intensità e carico cardiaco</h2>
      <p>
        Inserisci soglie crescenti nella stessa unità del segnale. Per la FC puoi indicare riposo, massimo
        e coefficienti TRIMP calibrati. Nessuna stima per lacune o parametri mancanti.
      </p>
      <form className="inline-form" onSubmit={submit}>
        <label>
          Canale
          <select name="metric">
            <option value="HeartRate">Frequenza cardiaca (bpm)</option>
            <option value="Speed">Velocità (m/s)</option>
            <option value="Power">Potenza (W)</option>
          </select>
        </label>
        <label>
          Soglie (virgola tra valori, punto decimale)
          <input name="thresholds" required maxLength={200} />
        </label>
        <label>
          FC riposo (bpm)
          <input name="resting" type="number" min={1} step="any" />
        </label>
        <label>
          FC massima (bpm)
          <input name="maximum" type="number" min={1} step="any" />
        </label>
        <label>
          Coefficiente TRIMP a
          <input name="a" type="number" min={0} step="any" />
        </label>
        <label>
          Coefficiente TRIMP b
          <input name="b" type="number" min={0} step="any" />
        </label>
        <button type="submit" disabled={analysis.isPending}>
          Analizza intensità
        </button>
      </form>
      {analysis.isPending && <p role="status">Analisi in corso…</p>}
      {analysis.isError && <p role="alert">Analisi non riuscita. Verifica soglie e parametri.</p>}
      {analysis.data && <Results report={analysis.data} />}
    </section>
  );
}
