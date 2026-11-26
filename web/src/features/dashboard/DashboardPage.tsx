import { lazy, Suspense } from "react";
import type { Weighing } from "../../api/client";
import { formatDateTime, formatDeltaKg, formatKg, formatRelative, freshness } from "../../lib/format";
import { useSetAttribution, useSummary, useWeighings } from "./queries";
import { StatCard } from "./StatCard";
import { DailySciencePanel } from "./DailySciencePanel";
import { DailySessionLoadPanel } from "./DailySessionLoadPanel";
import { AttestedRacePanel } from "./AttestedRacePanel";
import { RrChunkPanel } from "./RrChunkPanel";
import { PersonalAdaptationPanel } from "./PersonalAdaptationPanel";
import { PacingScenarioPanel } from "./PacingScenarioPanel";
import { PersonalResearchPanel } from "./PersonalResearchPanel";
import { ScienceFeedbackPanel } from "../science/ScienceFeedbackPanel";

// La libreria dei grafici è il pacchetto più pesante: caricata a parte, dopo i dati essenziali.
const WeightChart = lazy(() => import("./WeightChart").then((m) => ({ default: m.WeightChart })));

const attributionLabel: Record<Weighing["attribution"], string> = {
  Assumed: "Da confermare",
  Confirmed: "Mia",
  Rejected: "Di un'altra persona",
};

/** Azioni possibili per ogni stato: una pesata presunta si può confermare o escludere direttamente. */
const attributionActions: Record<Weighing["attribution"], { to: Weighing["attribution"]; label: string }[]> =
  {
    Assumed: [
      { to: "Confirmed", label: "È mia" },
      { to: "Rejected", label: "Non è mia" },
    ],
    Confirmed: [{ to: "Rejected", label: "Non è mia" }],
    Rejected: [{ to: "Confirmed", label: "È mia" }],
  };

function WeighingRow({ weighing }: { weighing: Weighing }) {
  const setAttribution = useSetAttribution();
  return (
    <tr className={weighing.attribution === "Rejected" ? "rejected" : undefined}>
      <td>{formatDateTime(weighing.measuredAt)}</td>
      <td className="num">{formatKg(weighing.weightKg)}</td>
      <td>{attributionLabel[weighing.attribution]}</td>
      <td>
        <div className="actions">
          {attributionActions[weighing.attribution].map((action) => (
            <button
              key={action.to}
              type="button"
              className="link"
              disabled={setAttribution.isPending}
              onClick={() => {
                setAttribution.mutate({ id: weighing.id, attribution: action.to });
              }}
            >
              {action.label}
            </button>
          ))}
        </div>
      </td>
    </tr>
  );
}

export function DashboardPage() {
  const summary = useSummary();
  const weighings = useWeighings();

  if (summary.isPending || weighings.isPending) return <p role="status">Caricamento…</p>;
  if (summary.isError || weighings.isError) return <p role="alert">Impossibile caricare i dati.</p>;

  const s = summary.data;
  const items = weighings.data.items;

  return (
    <>
      <h1>Forma</h1>
      <div className="stats">
        <StatCard
          label="Ultimo peso"
          value={formatKg(s.latestWeightKg)}
          hint={s.latestMeasuredAt ? formatRelative(s.latestMeasuredAt) : "nessuna pesata"}
        />
        <StatCard label="Tendenza 7 giorni" value={formatDeltaKg(s.trend7Days)} hint="media mobile" />
        <StatCard label="Tendenza 30 giorni" value={formatDeltaKg(s.trend30Days)} hint="media mobile" />
        <StatCard label="Pesate ultimi 30 giorni" value={s.weighingsLast30Days} />
      </div>

      <section className="card" aria-labelledby="trend-title">
        <h2 id="trend-title">Andamento</h2>
        <Suspense fallback={<p className="muted">Caricamento grafico…</p>}>
          <WeightChart items={items} />
        </Suspense>
      </section>

      <DailySciencePanel />
      <ScienceFeedbackPanel />
      <DailySessionLoadPanel />
      <AttestedRacePanel />
      <RrChunkPanel />
      <PersonalAdaptationPanel />
      <PacingScenarioPanel />
      <PersonalResearchPanel />

      <section className="card" aria-labelledby="sources-title">
        <h2 id="sources-title">Sorgenti dati</h2>
        {s.sources.length === 0 ? (
          <p className="muted">Nessun dispositivo collegato. Aggiungine uno dalla pagina Dispositivi.</p>
        ) : (
          <ul className="sources">
            {s.sources.map((src) => (
              // La freschezza dipende dai dati ricevuti, non dal semplice contatto dell'agente.
              <li key={src.deviceId} data-freshness={freshness(src.lastIngestedAt)}>
                <span className="dot" aria-hidden="true" />
                <span>
                  {src.name}: ultima pesata ricevuta {formatRelative(src.lastIngestedAt)}
                  <span className="muted"> · ultimo contatto {formatRelative(src.lastSeenAt)}</span>
                </span>
              </li>
            ))}
          </ul>
        )}
      </section>

      <section className="card" aria-labelledby="list-title">
        <h2 id="list-title">Pesate recenti</h2>
        {items.length === 0 ? (
          <p className="muted">Nessuna pesata negli ultimi 90 giorni.</p>
        ) : (
          <div className="table-scroll">
            <table>
              <thead>
                <tr>
                  <th scope="col">Quando</th>
                  <th scope="col" className="num">
                    Peso
                  </th>
                  <th scope="col">Attribuzione</th>
                  <th scope="col">
                    <span className="visually-hidden">Azioni</span>
                  </th>
                </tr>
              </thead>
              <tbody>
                {items.slice(0, 30).map((w) => (
                  <WeighingRow key={w.id} weighing={w} />
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </>
  );
}
