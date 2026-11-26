import type { SubmitEvent } from "react";
import { useMutation } from "@tanstack/react-query";
import { api, unwrap, type Schemas } from "../../api/client";

const number = new Intl.NumberFormat("it-IT", { maximumFractionDigits: 2 });
const format = (value: number | null) => (value == null ? "—" : number.format(value));

function Results({ report }: { report: Schemas["CalibratedTwinReport"] }) {
  return (
    <>
      <p>Stato: {report.status}</p>
      <dl>
        <dt>Tau calibrata (s)</dt>
        <dd>{format(report.parameters?.heartRateTimeConstantSeconds ?? null)}</dd>
        <dt>RMSE del recupero (bpm)</dt>
        <dd>{format(report.calibration.fit?.rootMeanSquaredErrorBpm ?? null)}</dd>
      </dl>
      <div className="table-scroll">
        <table>
          <thead>
            <tr>
              <th scope="col">Tempo cumulato (s)</th>
              <th scope="col">Distanza cumulata (m)</th>
              <th scope="col">FC finale simulata (bpm)</th>
              <th scope="col">FC media simulata (bpm)</th>
            </tr>
          </thead>
          <tbody>
            {report.segments.map((segment, index) => (
              <tr key={index}>
                <th scope="row">{number.format(segment.cumulativeSeconds)}</th>
                <td>{number.format(segment.cumulativeMeters)}</td>
                <td>{number.format(segment.endHeartRateBpm)}</td>
                <td>{number.format(segment.meanHeartRateBpm)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </>
  );
}

function segments(raw: string): Schemas["TwinSegment"][] {
  return raw.split("\n").map((line) => {
    const [seconds, speed, equilibrium] = line.split(";");
    return {
      durationSeconds: Number(seconds),
      externalSpeedMetersPerSecond: Number(speed),
      equilibriumHeartRateBpm: Number(equilibrium),
    };
  });
}

const fields = [
  ["offset", "Fine sforzo per calibrazione (s)"],
  ["equilibrium", "Equilibrio recupero dichiarato (bpm)"],
  ["initial", "FC iniziale scenario (bpm)"],
  ["rest", "FC riposo (bpm)"],
  ["max", "FC massima (bpm)"],
  ["rmse", "RMSE massimo accettato (bpm)"],
] as const;

export function CalibratedTwinPanel({ workoutId }: { workoutId: string }) {
  const analysis = useMutation({
    mutationFn: (body: Schemas["CalibratedTwinRequest"]) =>
      unwrap(api.POST("/api/v1/workouts/{id}/science/twin", { params: { path: { id: workoutId } }, body })),
  });
  const submit = (event: SubmitEvent<HTMLFormElement>) => {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    const raw = form.get("segments");
    const mode = form.get("mode");
    if (typeof raw !== "string" || typeof mode !== "string") return;
    analysis.mutate({
      recoveryProtocol: {
        endOffsetSeconds: Number(form.get("offset")),
        mode,
        confirmed: form.has("confirmed"),
        equilibriumBpm: Number(form.get("equilibrium")),
      },
      initialBpm: Number(form.get("initial")),
      restingBpm: Number(form.get("rest")),
      maximumBpm: Number(form.get("max")),
      maximumFitRmseBpm: Number(form.get("rmse")),
      transferAssumptionConfirmed: form.has("transfer"),
      segments: segments(raw.trim()),
    });
  };
  return (
    <section className="card" aria-labelledby="twin-title">
      <h2 id="twin-title">Digital Twin cardiaco condizionale</h2>
      <p>Usa tau del recupero registrato. Gli equilibri dei segmenti sono ipotesi dichiarate.</p>
      <p>Il trasferimento della tau allo sforzo non è una validazione fisiologica.</p>
      <form className="inline-form" onSubmit={submit}>
        {fields.map(([name, label]) => (
          <label key={name}>
            {label}
            <input name={name} type="number" min="0.001" step={name === "offset" ? 1 : "any"} required />
          </label>
        ))}
        <label>
          Modalità recupero
          <select name="mode">
            <option value="passive-seated">Passivo seduto</option>
            <option value="passive-standing">Passivo in piedi</option>
            <option value="passive-supine">Passivo supino</option>
            <option value="active-walking">Attivo camminando</option>
          </select>
        </label>
        <label>
          <input name="confirmed" type="checkbox" />
          Confermo la modalità costante di calibrazione
        </label>
        <label>
          Segmenti: secondi;m/s;FC equilibrio (uno per riga)
          <textarea name="segments" rows={4} maxLength={6000} required />
        </label>
        <label>
          <input name="transfer" type="checkbox" />
          Confermo ipotesi di trasferimento della tau
        </label>
        <button type="submit" disabled={analysis.isPending}>
          Simula risposta cardiaca
        </button>
      </form>
      {analysis.isPending && <p role="status">Analisi in corso…</p>}
      {analysis.isError && <p role="alert">Scenario non valido o non disponibile.</p>}
      {analysis.data && <Results report={analysis.data} />}
    </section>
  );
}
