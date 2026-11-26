import { useMutation } from "@tanstack/react-query";
import { useState, type SubmitEvent } from "react";
import { api, unwrap, type Schemas } from "../../api/client";

const number = new Intl.NumberFormat("it-IT", { maximumFractionDigits: 2 });
const format = (value: number | null) => (value == null ? "—" : number.format(value));

function Results({ report }: { report: Schemas["PacingScenarioReport"] }) {
  return (
    <>
      <p>Stato: {report.status}</p>
      <dl>
        <dt>Distanza (m)</dt>
        <dd>{number.format(report.distanceMeters)}</dd>
        <dt>Tempo obiettivo (s)</dt>
        <dd>{format(report.targetSeconds)}</dd>
        <dt>Fonte obiettivo</dt>
        <dd>{report.targetSource}</dd>
        <dt>Limite inferiore empirico (s)</dt>
        <dd>{format(report.interval?.lowerSeconds ?? null)}</dd>
        <dt>Limite superiore empirico (s)</dt>
        <dd>{format(report.interval?.upperSeconds ?? null)}</dd>
      </dl>
      <div className="table-scroll">
        <table>
          <thead>
            <tr>
              <th scope="col">Segmento</th>
              <th scope="col">Distanza (m)</th>
              <th scope="col">Tempo (s)</th>
              <th scope="col">Cumulato (s)</th>
              <th scope="col">Passo (s/km)</th>
            </tr>
          </thead>
          <tbody>
            {report.splits.map((split, index) => (
              <tr key={index}>
                <th scope="row">{index + 1}</th>
                <td>{number.format(split.distanceMeters)}</td>
                <td>{number.format(split.segmentSeconds)}</td>
                <td>{number.format(split.cumulativeSeconds)}</td>
                <td>{number.format((1000 * split.segmentSeconds) / split.distanceMeters)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </>
  );
}

function segments(raw: string): Schemas["PacingSegment"][] {
  return raw.split("\n").map((line) => {
    const [distance, cost] = line.split(";");
    return { distanceMeters: Number(distance), relativeTimeCost: Number(cost) };
  });
}

export function PacingScenarioPanel() {
  const [personal, setPersonal] = useState(false);
  const analysis = useMutation({
    mutationFn: (body: Schemas["PacingScenarioRequest"]) =>
      unwrap(api.POST("/api/v1/science/pacing", { body })),
  });
  const submit = (event: SubmitEvent<HTMLFormElement>) => {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    const text = form.get("segments");
    if (typeof text !== "string") return;
    const source = form.get("source");
    const from = form.get("from");
    const split = form.get("split");
    const until = form.get("until");
    const reference =
      personal &&
      typeof source === "string" &&
      typeof from === "string" &&
      typeof split === "string" &&
      typeof until === "string"
        ? { source, from, split, until }
        : null;
    analysis.mutate({
      segments: segments(text.trim()),
      targetSeconds: personal ? null : Number(form.get("target")),
      raceReference: reference,
    });
  };

  return (
    <section className="card" aria-labelledby="pacing-title">
      <h2 id="pacing-title">Scenario pacing</h2>
      <p>Costi relativi dichiarati, non correzioni fisiologiche automatiche. Non è una prescrizione.</p>
      <form className="inline-form" onSubmit={submit}>
        <label>
          Segmenti: metri;costo relativo (uno per riga)
          <textarea name="segments" required maxLength={6000} rows={4} placeholder="1000;1" />
        </label>
        <label>
          Usa previsione personale
          <input
            type="checkbox"
            checked={personal}
            onChange={(event) => {
              setPersonal(event.target.checked);
            }}
          />
        </label>
        {personal ? (
          <>
            <label>
              Sorgente prove pacing
              <input name="source" required maxLength={32} />
            </label>
            <label>
              Inizio fit pacing UTC
              <input name="from" type="date" required />
            </label>
            <label>
              Cutoff fit pacing UTC
              <input name="split" type="date" required />
            </label>
            <label>
              Fine calibrazione pacing UTC
              <input name="until" type="date" required />
            </label>
          </>
        ) : (
          <label>
            Tempo obiettivo (s)
            <input name="target" type="number" min="0.001" max={604800} step="any" required />
          </label>
        )}
        <button type="submit" disabled={analysis.isPending}>
          Simula pacing
        </button>
      </form>
      {analysis.isPending && <p role="status">Analisi in corso…</p>}
      {analysis.isError && <p role="alert">Scenario non valido o non disponibile.</p>}
      {analysis.data && <Results report={analysis.data} />}
    </section>
  );
}
