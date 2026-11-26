import type { SubmitEvent } from "react";
import type { Schemas } from "../../api/client";
import { formatDateTime } from "../../lib/format";
import { useAnalyzeDurability } from "./queries";

const number = new Intl.NumberFormat("it-IT", { maximumFractionDigits: 2 });
const percent = new Intl.NumberFormat("it-IT", { style: "percent", maximumFractionDigits: 1 });

function Results({ report }: { report: Schemas["WorkoutDurabilityReport"] }) {
  const profile = report.profile;
  const unit = report.protocol.metric === "Speed" ? "m/s" : "W";
  return (
    <>
      <p className="muted">
        Revisione {report.revision} · {report.methodVersion} · gap massimo {report.maxGapSeconds} s.
      </p>
      {report.status === "missing-channel" ? (
        <p>Canale di carico o frequenza cardiaca non acquisito.</p>
      ) : !profile ? (
        <p>Supporto accoppiato insufficiente: nessuna stima del deterioramento.</p>
      ) : (
        <>
          <p>Trend: {number.format(profile.lossPercentagePointsPerHour)} punti percentuali/ora.</p>
          <p>
            {profile.onset == null || profile.onsetConfirmedAt == null
              ? "Nessun onset persistente osservato."
              : `Onset: ${formatDateTime(profile.onset)} · confermato: ${formatDateTime(profile.onsetConfirmedAt)}.`}
          </p>
        </>
      )}
      {report.windows.length > 0 && (
        <div className="table-scroll">
          <table>
            <thead>
              <tr>
                <th scope="col">Inizio (s)</th>
                <th scope="col">Copertura comune</th>
                <th scope="col">Carico ({unit})</th>
                <th scope="col">FC (bpm)</th>
                <th scope="col">Perdita (%)</th>
              </tr>
            </thead>
            <tbody>
              {report.windows.map((window, index) => (
                <tr key={window.offsetSeconds}>
                  <th scope="row">{window.offsetSeconds}</th>
                  <td>{percent.format(window.coverage)}</td>
                  <td>{window.meanExternalLoad == null ? "—" : number.format(window.meanExternalLoad)}</td>
                  <td>{window.meanHeartRateBpm == null ? "—" : number.format(window.meanHeartRateBpm)}</td>
                  <td>
                    {profile?.lossPercent[index] == null ? "—" : number.format(profile.lossPercent[index])}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </>
  );
}

export function WorkoutDurabilityPanel({ workoutId, duration }: { workoutId: string; duration: number }) {
  const analysis = useAnalyzeDurability(workoutId);
  const submit = (event: SubmitEvent<HTMLFormElement>) => {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    analysis.mutate({
      metric: form.get("metric") === "Power" ? "Power" : "Speed",
      startOffsetSeconds: Number(form.get("start")),
      windowSeconds: Number(form.get("seconds")),
      windowCount: Number(form.get("count")),
      minimumCoverage: Number(form.get("coverage")) / 100,
      onsetLossPercent: Number(form.get("loss")),
      minimumConsecutiveWindows: Number(form.get("consecutive")),
    });
  };

  return (
    <section className="card" aria-labelledby="durability-title">
      <h2 id="durability-title">Durability: disaccoppiamento cardiaco</h2>
      <p>
        Seleziona un tratto comparabile, senza riscaldamento, pause o cambi di condizioni. Le soglie sono
        parametri descrittivi, non limiti fisiologici o diagnosi.
      </p>
      {duration < 90 ? (
        <p>Seduta troppo breve per tre finestre di almeno 30 s.</p>
      ) : (
        <form className="inline-form" onSubmit={submit}>
          <label>
            Carico esterno
            <select name="metric">
              <option value="Speed">Velocità</option>
              <option value="Power">Potenza</option>
            </select>
          </label>
          <label>
            Inizio (s)
            <input name="start" type="number" min={0} max={Math.floor(duration)} defaultValue={0} required />
          </label>
          <label>
            Durata finestra (s)
            <input
              name="seconds"
              type="number"
              min={30}
              max={3600}
              defaultValue={Math.min(300, Math.floor(duration / 3))}
              required
            />
          </label>
          <label>
            Numero finestre
            <input name="count" type="number" min={3} max={30} defaultValue={3} required />
          </label>
          <label>
            Copertura minima (%)
            <input name="coverage" type="number" min={1} max={100} defaultValue={80} required />
          </label>
          <label>
            Soglia perdita (%)
            <input name="loss" type="number" min={0.1} max={100} step={0.1} defaultValue={5} required />
          </label>
          <label>
            Finestre consecutive
            <input name="consecutive" type="number" min={1} max={29} defaultValue={2} required />
          </label>
          <button type="submit" disabled={analysis.isPending}>
            Analizza durability
          </button>
        </form>
      )}
      {analysis.isPending && <p role="status">Analisi in corso…</p>}
      {analysis.isError && <p role="alert">Analisi non riuscita. Verifica il protocollo e riprova.</p>}
      {analysis.data && <Results report={analysis.data} />}
    </section>
  );
}
