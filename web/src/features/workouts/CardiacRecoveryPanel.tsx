import { useMutation } from "@tanstack/react-query";
import type { SubmitEvent } from "react";
import { api, unwrap, type Schemas } from "../../api/client";

interface Protocol {
  endOffsetSeconds: number;
  mode: string;
  confirmed: boolean;
  equilibriumBpm?: number;
}

const number = new Intl.NumberFormat("it-IT", { maximumFractionDigits: 2 });
const format = (value: number | null) => (value == null ? "—" : number.format(value));

function Report({ report }: { report: Schemas["WorkoutCardiacRecoveryReport"] }) {
  return (
    <>
      <p className="muted">
        Revisione {report.revision} · {report.methodVersion}
      </p>
      <p>Stato: {report.status}. Fit: {report.fitStatus}.</p>
      <dl>
        <dt>Costante di tempo (s)</dt>
        <dd>{format(report.fit?.timeConstantSeconds ?? null)}</dd>
        <dt>RMSE del fit (bpm)</dt>
        <dd>{format(report.fit?.rootMeanSquaredErrorBpm ?? null)}</dd>
      </dl>
      <div className="table-scroll">
        <table>
          <thead>
            <tr>
              <th scope="col">Tempo dal termine (s)</th>
              <th scope="col">FC (bpm)</th>
              <th scope="col">Riduzione FC (bpm)</th>
              <th scope="col">Istante osservato</th>
            </tr>
          </thead>
          <tbody>
            {report.points.map((point) => (
              <tr key={point.elapsedSeconds}>
                <th scope="row">{point.elapsedSeconds}</th>
                <td>{format(point.heartRateBpm)}</td>
                <td>{format(point.dropFromEndBpm)}</td>
                <td>{point.observedAt ?? "—"}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </>
  );
}

export function CardiacRecoveryPanel({ workoutId }: { workoutId: string }) {
  const analysis = useMutation({
    mutationFn: (query: Protocol) => {
      const params = { path: { id: workoutId }, query };
      return unwrap(api.GET("/api/v1/workouts/{id}/science/cardiac-recovery", { params }));
    },
  });
  const submit = (event: SubmitEvent<HTMLFormElement>) => {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    const mode = form.get("mode");
    const equilibrium = form.get("equilibrium");
    if (typeof mode !== "string") return;
    const query: Protocol = {
      endOffsetSeconds: Number(form.get("offset")),
      mode,
      confirmed: form.has("confirmed"),
    };
    if (typeof equilibrium === "string" && equilibrium !== "") query.equilibriumBpm = Number(equilibrium);
    analysis.mutate(query);
  };

  return (
    <section className="card" aria-labelledby="cardiac-recovery-title">
      <h2 id="cardiac-recovery-title">Recupero cardiaco</h2>
      <p>Indica la fine dello sforzo dentro la registrazione, non la fine del file.</p>
      <p>La FC di equilibrio è facoltativa e deve provenire da un protocollo separato.</p>
      <form className="inline-form" onSubmit={submit}>
        <label>
          Fine sforzo dall'inizio della seduta (s)
          <input name="offset" type="number" min={1} required />
        </label>
        <label>
          Modalità di recupero
          <select name="mode">
            <option value="passive-seated">Passivo seduto</option>
            <option value="passive-standing">Passivo in piedi</option>
            <option value="passive-supine">Passivo supino</option>
            <option value="active-walking">Attivo camminando</option>
          </select>
        </label>
        <label>
          FC di equilibrio (bpm)
          <input name="equilibrium" type="number" min={1} step="any" />
        </label>
        <label>
          <input name="confirmed" type="checkbox" />
          Confermo la modalità costante di recupero
        </label>
        <button type="submit" disabled={analysis.isPending}>
          Analizza recupero cardiaco
        </button>
      </form>
      {analysis.isPending && <p role="status">Analisi in corso…</p>}
      {analysis.isError && <p role="alert">Analisi cardiaca non riuscita.</p>}
      {analysis.data && <Report report={analysis.data} />}
    </section>
  );
}
