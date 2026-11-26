import { useMutation } from "@tanstack/react-query";
import type { SubmitEvent } from "react";
import { api, unwrap, type Schemas } from "../../api/client";

const number = new Intl.NumberFormat("it-IT", { maximumFractionDigits: 3 });
const percent = new Intl.NumberFormat("it-IT", { style: "percent", maximumFractionDigits: 1 });
const format = (value: number | null) => (value == null ? "—" : number.format(value));

const statuses: Record<string, string> = {
  observed: "Supporto comune osservato",
  "missing-channel": "Canali di velocità o cadenza mancanti",
  "insufficient-support": "Supporto comune insufficiente",
  "unconfirmed-cadence-unit": "Unità della cadenza non confermata",
  "unsupported-activity": "Attività non compatibile",
};

function Report({ report }: { report: Schemas["RunningStepReport"] }) {
  return (
    <>
      <p>{statuses[report.status] ?? report.status}</p>
      <p className="muted">
        Revisione {report.revision} · {report.methodVersion} · gap massimo {report.maxGapSeconds} s.
      </p>
      <dl>
        <dt>Copertura comune</dt>
        <dd>{percent.format(report.coverage)}</dd>
        <dt>Velocità media (m/s)</dt>
        <dd>{format(report.meanSpeedMetersPerSecond)}</dd>
        <dt>Cadenza media (passi/min)</dt>
        <dd>{format(report.meanCadenceStepsPerMinute)}</dd>
        <dt>Lunghezza media stimata del passo (m)</dt>
        <dd>{format(report.meanStepLengthMeters)}</dd>
        <dt>Variabilità temporale del passo (CV %)</dt>
        <dd>{format(report.stepLengthCvPercent)}</dd>
        <dt>Variabilità temporale della cadenza (CV %)</dt>
        <dd>{format(report.cadenceCvPercent)}</dd>
      </dl>
    </>
  );
}

export function RunningStepPanel({ workoutId }: { workoutId: string }) {
  const analysis = useMutation({
    mutationFn: (cadenceIsStepsPerMinute: boolean) =>
      unwrap(
        api.GET("/api/v1/workouts/{id}/science/steps", {
          params: { path: { id: workoutId }, query: { cadenceIsStepsPerMinute } },
        }),
      ),
  });
  const submit = (event: SubmitEvent<HTMLFormElement>) => {
    event.preventDefault();
    analysis.mutate(new FormData(event.currentTarget).has("confirmed"));
  };

  return (
    <section className="card" aria-labelledby="running-step-title">
      <h2 id="running-step-title">Biomeccanica: passo e cadenza</h2>
      <p>
        Stima cinematica sui soli intervalli con velocità e cadenza valide. Non misura la variabilità
        dei singoli passi né la simmetria dei due arti.
      </p>
      <form className="inline-form" onSubmit={submit}>
        <label>
          <input name="confirmed" type="checkbox" />
          Confermo che la cadenza è in passi totali al minuto
        </label>
        <button type="submit" disabled={analysis.isPending}>
          Analizza biomeccanica
        </button>
      </form>
      {analysis.isPending && <p role="status">Analisi in corso…</p>}
      {analysis.isError && <p role="alert">Analisi biomeccanica non riuscita.</p>}
      {analysis.data && <Report report={analysis.data} />}
    </section>
  );
}
