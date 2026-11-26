import { useMutation } from "@tanstack/react-query";
import type { SubmitEvent } from "react";
import { api, unwrap, type Schemas } from "../../api/client";

interface Protocol {
  source: string;
  day: string;
  context: string;
  screened: boolean;
  timestampsAreBeatEnds: boolean;
  chunkId?: string;
}

const number = new Intl.NumberFormat("it-IT", { maximumFractionDigits: 3 });
const format = (value: number | null) => (value == null ? "—" : number.format(value));

function Analysis({ report }: { report: Schemas["RrChunkAnalysis"] }) {
  return (
    <>
      <p>Stato: {report.status}</p>
      <dl>
        <dt>Durata degli intervalli (s)</dt>
        <dd>{format(report.intervalDurationSeconds)}</dd>
        <dt>RMSSD a riposo (ms)</dt>
        <dd>{format(report.hrv?.rmssdMilliseconds ?? null)}</dd>
        <dt>SDNN a riposo (ms)</dt>
        <dd>{format(report.hrv?.sdnnMilliseconds ?? null)}</dd>
        <dt>DFA alpha1</dt>
        <dd>{format(report.dfa?.alpha1 ?? null)}</dd>
      </dl>
      <p className="muted">
        {report.hrvMethodVersion} · {report.dfaMethodVersion}
      </p>
    </>
  );
}

export function RrChunkPanel() {
  const analysis = useMutation({
    mutationFn: (query: Protocol) => unwrap(api.GET("/api/v1/science/rr", { params: { query } })),
  });

  const submit = (event: SubmitEvent<HTMLFormElement>) => {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    const source = form.get("source");
    const day = form.get("day");
    const context = form.get("context");
    const chunkId = form.get("chunk");
    if (typeof source !== "string" || typeof day !== "string" || typeof context !== "string") return;
    const query: Protocol = {
      source,
      day,
      context,
      screened: form.has("screened"),
      timestampsAreBeatEnds: form.has("beatEnds"),
    };
    if (typeof chunkId === "string" && chunkId !== "") query.chunkId = chunkId;
    analysis.mutate(query);
  };
  const report = analysis.data;
  return (
    <section className="card" aria-labelledby="rr-chunk-title">
      <h2 id="rr-chunk-title">Intervalli RR, HRV e DFA alpha1</h2>
      <p>Seleziona una revisione precisa di intervalli NN consecutivi, mai ricavati dalla FC.</p>
      <p>Conferma lo screening solo se sono stati verificati artefatti e battiti ectopici.</p>
      <form className="inline-form" onSubmit={submit}>
        <label>
          Sorgente RR
          <input name="source" required maxLength={200} />
        </label>
        <label>
          Giorno UTC del primo campione
          <input name="day" type="date" required />
        </label>
        <label>
          Contesto
          <select name="context">
            <option value="resting">Riposo</option>
            <option value="exercise">Sforzo</option>
          </select>
        </label>
        <label>
          Revisione chunk
          <select name="chunk" key={report?.day ?? "empty"}>
            <option value="">Elenca revisioni disponibili</option>
            {report?.chunks.map((chunk) => (
              <option key={chunk.id} value={chunk.id}>
                {chunk.externalId} · {chunk.revision} · {chunk.firstSample}
              </option>
            ))}
          </select>
        </label>
        <label>
          <input name="screened" type="checkbox" />
          Intervalli NN consecutivi verificati
        </label>
        <label>
          <input name="beatEnds" type="checkbox" />
          Timestamp a fine intervallo confermati
        </label>
        <button type="submit" disabled={analysis.isPending}>
          Leggi intervalli RR
        </button>
      </form>
      {analysis.isPending && <p role="status">Analisi in corso…</p>}
      {analysis.isError && <p role="alert">Analisi RR non riuscita.</p>}
      {report && report.chunks.length === 0 && <p>Nessuna sequenza RR disponibile.</p>}
      {report?.truncated && <p>Elenco limitato alle prime 64 revisioni.</p>}
      {report?.selected && (
        <p>
          Revisione {report.selected.revision} · {report.selected.method} · {report.selected.sampleCount}{" "}
          campioni
        </p>
      )}
      {report?.analysis && <Analysis report={report.analysis} />}
    </section>
  );
}
