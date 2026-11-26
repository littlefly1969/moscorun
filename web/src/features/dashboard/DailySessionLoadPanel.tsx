import { useMutation } from "@tanstack/react-query";
import { useState, type SubmitEvent } from "react";
import { api, unwrap, type Schemas } from "../../api/client";

const labels: Record<string, string> = {
  observed: "Osservato",
  "observed-rest": "Riposo confermato",
  "missing-attestation": "Diario non confermato",
  "ambiguous-attestation": "Revisioni ambigue",
  "incomplete-attestation": "Diario incompleto",
  "changed-after-attestation": "Allenamenti aggiornati dopo conferma",
  "cross-day-session": "Allenamento a cavallo di due giorni",
  "overlapping-sessions": "Allenamenti sovrapposti",
  "missing-session-feedback": "Feedback sessione mancante",
  "invalid-load": "Carico non valido",
};

const number = new Intl.NumberFormat("it-IT", { maximumFractionDigits: 2 });
const format = (value: number | null) => (value == null ? "—" : number.format(value));

function Report({ report }: { report: Schemas["DailySessionLoadReport"] }) {
  const week = report.recentWeek;
  const rolling = report.rolling;
  return (
    <>
      <p className="muted">
        {report.methodVersion} · dati disponibili al {report.asOf} · unità {report.unit}
      </p>
      <dl>
        <dt>Carico ultimi 7 giorni</dt>
        <dd>{format(week?.totalLoad ?? null)}</dd>
        <dt>Media giornaliera (7 giorni)</dt>
        <dd>{format(week?.meanDailyLoad ?? null)}</dd>
        <dt>Monotonia</dt>
        <dd>{format(week?.monotony ?? null)}</dd>
        <dt>Strain</dt>
        <dd>{format(week?.strain ?? null)}</dd>
        <dt>Media dei 28 giorni precedenti</dt>
        <dd>{format(rolling?.priorMean ?? null)}</dd>
        <dt>Rapporto medie 7 / 28 giorni precedenti</dt>
        <dd>{format(rolling?.recentToPriorRatio ?? null)}</dd>
      </dl>
      {!week && <p>Servono 7 giornate complete per il riepilogo settimanale.</p>}
      {!rolling && <p>Servono 35 giornate complete per il confronto tra finestre disgiunte.</p>}
      <div className="table-scroll">
        <table>
          <thead>
            <tr>
              <th scope="col">Giorno UTC</th>
              <th scope="col">Sedute con RPE / totali</th>
              <th scope="col">Carico</th>
              <th scope="col">Qualità</th>
            </tr>
          </thead>
          <tbody>
            {report.days.map((day) => (
              <tr key={day.day}>
                <th scope="row">{day.day}</th>
                <td>{day.ratedSessions} / {day.sessions}</td>
                <td>{format(day.load)}</td>
                <td>{labels[day.status] ?? day.status}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </>
  );
}

export function DailySessionLoadPanel() {
  const analysis = useMutation({
    mutationFn: (last: string) =>
      unwrap(api.GET("/api/v1/science/load", { params: { query: { last } } })),
  });
  const [yesterday] = useState(() => new Date(Date.now() - 86400000).toISOString().slice(0, 10));
  const submit = (event: SubmitEvent<HTMLFormElement>) => {
    event.preventDefault();
    const last = new FormData(event.currentTarget).get("last");
    if (typeof last === "string") analysis.mutate(last);
  };

  return (
    <section className="card" aria-labelledby="session-load-title">
      <h2 id="session-load-title">Carico di allenamento longitudinale</h2>
      <p>
        Il carico deriva dalla durata attiva e dall'RPE delle sedute. Conferma il diario giornaliero solo a
        giornata UTC conclusa. Un giorno senza dati non è un giorno di riposo. Nessun punteggio di rischio.
      </p>
      <form className="inline-form" onSubmit={submit}>
        <label>
          Ultimo giorno UTC
          <input name="last" type="date" max={yesterday} defaultValue={yesterday} required />
        </label>
        <button type="submit" disabled={analysis.isPending}>
          Analizza carico
        </button>
      </form>
      {analysis.isPending && <p role="status">Analisi in corso…</p>}
      {analysis.isError && <p role="alert">Analisi del carico non riuscita.</p>}
      {analysis.data && <Report report={analysis.data} />}
    </section>
  );
}
