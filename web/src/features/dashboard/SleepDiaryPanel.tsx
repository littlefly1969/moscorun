import type { SubmitEvent } from "react";
import { useMutation } from "@tanstack/react-query";
import { api, unwrap, type Schemas } from "../../api/client";

const number = new Intl.NumberFormat("it-IT", { maximumFractionDigits: 1 });
const format = (value: number | null | undefined) => (value == null ? "—" : number.format(value));

function clock(value: number | null | undefined) {
  if (value == null) return "—";
  const minute = Math.round(value) % 1440;
  return [Math.floor(minute / 60), minute % 60].map((part) => part.toString().padStart(2, "0")).join(":");
}

function Results({ report }: { report: Schemas["SleepDiaryReport"] }) {
  return (
    <>
      <p>Stato: {report.status}</p>
      <p>
        Notti complete: {report.completeNights}/{report.windowDays}
      </p>
      <dl>
        <dt>Ora media a letto (locale)</dt>
        <dd>{clock(report.bedtime?.meanMinuteOfDay)}</dd>
        <dt>Variabilità circolare a letto (min)</dt>
        <dd>{format(report.bedtime?.circularStandardDeviationMinutes)}</dd>
        <dt>Ora media risveglio (locale)</dt>
        <dd>{clock(report.wake?.meanMinuteOfDay)}</dd>
        <dt>Variabilità circolare risveglio (min)</dt>
        <dd>{format(report.wake?.circularStandardDeviationMinutes)}</dd>
        <dt>Efficienza media dichiarata (%)</dt>
        <dd>{format(report.meanEfficiencyPercent)}</dd>
      </dl>
      <div className="table-scroll">
        <table>
          <thead>
            <tr>
              <th scope="col">Giorno UTC</th>
              <th scope="col">Stato</th>
              <th scope="col">A letto</th>
              <th scope="col">Risveglio</th>
              <th scope="col">Efficienza (%)</th>
            </tr>
          </thead>
          <tbody>
            {report.nights.map((night) => (
              <tr key={night.day}>
                <th scope="row">{night.day}</th>
                <td>{night.status}</td>
                <td>{clock(night.bedtimeMinute)}</td>
                <td>{clock(night.wakeMinute)}</td>
                <td>{format(night.efficiencyPercent)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </>
  );
}

export function SleepDiaryPanel() {
  const analysis = useMutation({
    mutationFn: (query: { last: string; days: number }) =>
      unwrap(api.GET("/api/v1/science/sleep", { params: { query } })),
  });
  const submit = (event: SubmitEvent<HTMLFormElement>) => {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    const last = form.get("last");
    if (typeof last !== "string") return;
    analysis.mutate({ last, days: Number(form.get("days")) });
  };
  return (
    <section className="card" aria-labelledby="sleep-diary-title">
      <h2 id="sleep-diary-title">Regolarità del sonno</h2>
      <p>Notte principale conclusa nel giorno UTC, orari locali nello stesso fuso abituale.</p>
      <p>Compila il diario giornaliero. Servono almeno sette notti complete.</p>
      <form className="inline-form" onSubmit={submit}>
        <label>
          Ultimo giorno sonno UTC
          <input name="last" type="date" required />
        </label>
        <label>
          Finestra sonno (giorni)
          <input name="days" type="number" min={7} max={90} defaultValue={28} required />
        </label>
        <button type="submit" disabled={analysis.isPending}>
          Analizza sonno
        </button>
      </form>
      {analysis.isPending && <p role="status">Analisi in corso…</p>}
      {analysis.isError && <p role="alert">Analisi sonno non disponibile.</p>}
      {analysis.data && <Results report={analysis.data} />}
    </section>
  );
}
