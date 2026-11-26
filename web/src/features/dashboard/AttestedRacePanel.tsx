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

function Performance({ profile }: { profile: Schemas["AttestedPerformanceProfile"] }) {
  const fit = profile.robustCriticalSpeed;
  return (
    <section aria-label="Profilo fisiologico di campo">
      <h3>Velocità critica e test di Cooper</h3>
      <dl>
        <dt>Velocità critica robusta (m/s)</dt>
        <dd>{format(fit?.criticalSpeedMetersPerSecond ?? null)}</dd>
        <dt>Riserva di distanza (m)</dt>
        <dd>{format(fit?.distanceReserveMeters ?? null)}</dd>
        <dt>RMSE robusto (m)</dt>
        <dd>{format(fit?.rootMeanSquaredErrorMeters ?? null)}</dd>
        <dt>Velocità critica OLS (m/s)</dt>
        <dd>{format(profile.ordinaryLeastSquares?.criticalSpeedMetersPerSecond ?? null)}</dd>
        <dt>Potenza critica (W)</dt>
        <dd>{format(profile.criticalPower?.criticalWatts ?? null)}</dd>
        <dt>Riserva di lavoro (J)</dt>
        <dd>{format(profile.criticalPower?.workReserveJoules ?? null)}</dd>
        <dt>RMSE del lavoro (J)</dt>
        <dd>{format(profile.criticalPower?.rootMeanSquaredErrorJoules ?? null)}</dd>
      </dl>
      {!fit && <p>Prove distinte da 2 a 20 minuti insufficienti per il fit robusto.</p>}
      <p>Cooper: stima di campo, non misura diretta del VO2max.</p>
      {profile.cooperTests.length === 0 ? (
        <p>Nessuna prova massimale attestata di 12 minuti.</p>
      ) : (
        <ul>
          {profile.cooperTests.map((item) => (
            <li key={item.workoutId}>
              {item.endedAt}: {number.format(item.estimatedVo2MaxMlKgMin)} mL/kg/min
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}

function Report({ report }: { report: Schemas["AttestedRaceReport"] }) {
  return (
    <>
      <p className="muted">
        {report.methodVersion} · {report.protocol}
      </p>
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
      <Performance profile={report.performance} />
      {!report.model && <p>Prove massimali disponibili al cutoff insufficienti.</p>}
      {report.model && !report.prediction && <p>Distanza fuori dal supporto del modello.</p>}
      {report.prediction && !report.interval && <p>Intervallo non calibrabile.</p>}
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
    mutationFn: (query: Protocol) =>
      unwrap(api.GET("/api/v1/science/race-calibration", { params: { query } })),
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
