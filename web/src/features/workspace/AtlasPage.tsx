import { useQueries } from "@tanstack/react-query";
import { useState, type SubmitEvent } from "react";
import { Link } from "react-router";
import { api, unwrap, type Schemas } from "../../api/client";
import { metricCatalog, metricFamilies, type MetricDefinition } from "./metricCatalog";

interface Scope {
  source: string;
  day: string;
  baselineDays: number;
}

type Report = Schemas["DailyMetricReport"];

const number = new Intl.NumberFormat("it-IT", { maximumFractionDigits: 2 });
const dailyMetrics = metricCatalog.filter((metric) => metric.access === "daily");
const statuses: Record<string, string> = {
  observed: "Osservato",
  missing: "Non disponibile",
  ambiguous: "Ambiguo",
  invalid: "Non valido",
  "incompatible-method": "Metodo non comparabile",
  "insufficient-baseline": "Baseline insufficiente",
};
const statusLabel = (status: string) => statuses[status] ?? status;

function MetricHistory({ report }: { report: Report }) {
  const history = [...report.history, report.current].sort((a, b) => a.day.localeCompare(b.day));
  const observed = history.filter((point) => point.status === "observed" && point.value != null);
  const values = observed.flatMap((point) => (point.value == null ? [] : [point.value]));
  const low = values.length > 0 ? Math.min(...values) : 0;
  const high = values.length > 0 ? Math.max(...values) : 0;
  const start = Date.parse(history[0]?.day ?? report.current.day);
  const end = Date.parse(report.current.day);
  return (
    <>
      <div className="sw-value">
        {report.current.status === "observed" && report.current.value != null
          ? number.format(report.current.value)
          : "—"}{" "}
        <small>{report.unit}</small>
      </div>
      <p>{statusLabel(report.status)}</p>
      <dl className="sw-meta">
        <dt>Sorgente</dt>
        <dd>{report.source}</dd>
        <dt>Giorno UTC</dt>
        <dd>{report.current.day}</dd>
        <dt>Metodo</dt>
        <dd>{report.current.method ?? "Non disponibile"}</dd>
        <dt>Versione analisi</dt>
        <dd>{report.methodVersion}</dd>
        <dt>Disponibilità</dt>
        <dd>{report.current.availableAt ?? "Non disponibile"}</dd>
        <dt>Cutoff</dt>
        <dd>{report.asOf}</dd>
        <dt>Mediana personale</dt>
        <dd>{report.baseline ? number.format(report.baseline.baselineMedian) : "—"}</dd>
        <dt>Giorni osservati nella finestra</dt>
        <dd>
          {observed.length}/{history.length}
        </dd>
      </dl>
      {values.length > 0 && (
        <figure className="sw-figure">
          <svg viewBox="0 0 320 112" role="img" aria-label="Storico dei campioni giornalieri osservati">
            <path d="M8 8V100H312" fill="none" stroke="currentColor" opacity=".25" />
            {observed.map((point) => {
              if (point.value == null) return null;
              const x = 12 + (296 * (Date.parse(point.day) - start)) / Math.max(86400000, end - start);
              const y = 94 - (82 * (point.value - low)) / Math.max(1e-9, high - low);
              return (
                <circle key={point.day} cx={x} cy={high === low ? 53 : y} r="3" fill="currentColor">
                  <title>{point.day + ": " + number.format(point.value)}</title>
                </circle>
              );
            })}
          </svg>
          <figcaption>Campioni osservati, nessuna interpolazione dei giorni mancanti.</figcaption>
        </figure>
      )}
      <details>
        <summary>Valori e qualità della serie</summary>
        <div className="table-scroll">
          <table>
            <thead>
              <tr>
                <th scope="col">Giorno UTC</th>
                <th scope="col">Valore</th>
                <th scope="col">Qualità</th>
              </tr>
            </thead>
            <tbody>
              {history.map((point) => (
                <tr key={point.day}>
                  <th scope="row">{point.day}</th>
                  <td>{point.value == null ? "—" : number.format(point.value)}</td>
                  <td>{statusLabel(point.status)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </details>
    </>
  );
}

function MetricInspector({
  metric,
  report,
  state,
}: {
  metric: MetricDefinition;
  report: Report | undefined;
  state: string;
}) {
  return (
    <aside className="sw-inspector" aria-label="Dettaglio metrica">
      <p className="sw-kicker">{metricFamilies[metric.family]}</p>
      <h2>{metric.label}</h2>
      <p className="sw-muted">
        {metric.id} · {metric.unit}
      </p>
      {metric.family === "providerEstimates" && (
        <p>Stima del dispositivo: non equivale a una misurazione fisiologica diretta.</p>
      )}
      {report ? <MetricHistory report={report} /> : <p>{state}</p>}
      {metric.access === "workout" && <Link to="/cronoscopio">Esplora i campioni della seduta →</Link>}
      {metric.access === "diary" && <Link to="/laboratorio?metodo=diario">Apri il diario →</Link>}
      {metric.id === "weight" && <Link to="/forma">Consulta anche le pesate della bilancia →</Link>}
      {metric.access === "catalog" && (
        <p className="sw-muted">
          Il catalogo descrive una capacità prevista. La lettura non è ancora collegata a questa vista.
          Non si può dedurre che il sensore non la registri.
        </p>
      )}
    </aside>
  );
}

export function AtlasPage() {
  const [search, setSearch] = useState("");
  const [family, setFamily] = useState("all");
  const [selected, setSelected] = useState("restingHeartRate");
  const [favorites, setFavorites] = useState<string[]>([]);
  const [onlyFavorites, setOnlyFavorites] = useState(false);
  const [scope, setScope] = useState<Scope | null>(null);
  const [yesterday] = useState(() => new Date(Date.now() - 86400000).toISOString().slice(0, 10));
  const queries = useQueries({
    queries: dailyMetrics.map((metric) => ({
      queryKey: ["workspace", "daily", metric.id, scope],
      queryFn: () => {
        if (!scope) throw new Error("Contesto non selezionato");
        return unwrap(
          api.GET("/api/v1/science/daily", { params: { query: { metric: metric.id, ...scope } } }),
        );
      },
      enabled: scope != null,
    })),
  });
  const reports = new Map(dailyMetrics.map((metric, index) => [metric.id, queries[index]]));
  const lookup = (metric: MetricDefinition) => {
    const query = reports.get(metric.id);
    if (metric.access === "workout") return "Serie della seduta";
    if (metric.access === "diary") return "Diario dichiarato";
    if (metric.access === "catalog") return "Lettura non collegata";
    if (!scope) return "Seleziona fonte e giorno";
    if (query?.isFetching) return "Caricamento…";
    if (query?.isError) return "Errore di lettura";
    return query?.data ? statusLabel(query.data.current.status) : "Non interrogato";
  };
  const normalized = search.trim().toLocaleLowerCase("it-IT");
  const visible = metricCatalog.filter(
    (metric) =>
      (family === "all" || metric.family === family) &&
      (!onlyFavorites || favorites.includes(metric.id)) &&
      (metric.label + " " + metric.id + " " + metric.unit).toLocaleLowerCase("it-IT").includes(normalized),
  );
  const current = visible.find((metric) => metric.id === selected) ?? visible[0];
  const submit = (event: SubmitEvent<HTMLFormElement>) => {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    const source = form.get("source");
    const day = form.get("day");
    if (typeof source !== "string" || typeof day !== "string" || !source.trim()) return;
    setScope({ source: source.trim(), day, baselineDays: Number(form.get("baselineDays")) });
  };
  return (
    <>
      <header className="sw-head">
        <p className="sw-kicker">01 / Osservatorio scientifico</p>
        <h1>Atlante delle metriche</h1>
        <p>Il patrimonio osservabile dell’atleta. Ogni misura conserva fonte, metodo e disponibilità.</p>
      </header>
      <div className="sw-index">
        <span>
          <strong>{metricCatalog.length}</strong> metriche
        </span>
        <span>
          <strong>{Object.keys(metricFamilies).length}</strong> famiglie
        </span>
        <span>
          <strong>{dailyMetrics.length}</strong> interrogabili per giorno
        </span>
        <span>
          <strong>{queries.filter((query) => query.data?.current.status === "observed").length}</strong>{" "}
          osservate nel contesto
        </span>
      </div>
      <form className="sw-controls" onSubmit={submit}>
        <label>
          Sorgente scientifica
          <input name="source" required maxLength={200} defaultValue="samsung-health" />
        </label>
        <label>
          Giorno UTC
          <input name="day" type="date" max={yesterday} defaultValue={yesterday} required />
        </label>
        <label>
          Baseline (giorni)
          <input name="baselineDays" type="number" min={7} max={90} defaultValue={28} required />
        </label>
        <button type="submit">Interroga metriche giornaliere</button>
      </form>
      {scope && (
        <p className="sw-muted" role="status">
          Contesto attivo: {scope.source} · {scope.day} UTC · baseline {scope.baselineDays} giorni.
        </p>
      )}
      <div className="sw-controls">
        <label>
          Cerca metrica
          <input value={search} onChange={(event) => setSearch(event.target.value)} type="search" />
        </label>
        <label>
          Famiglia
          <select value={family} onChange={(event) => setFamily(event.target.value)}>
            <option value="all">Tutte le famiglie</option>
            {Object.entries(metricFamilies).map(([id, label]) => (
              <option key={id} value={id}>
                {label}
              </option>
            ))}
          </select>
        </label>
        <label className="sw-check">
          <input
            type="checkbox"
            checked={onlyFavorites}
            onChange={(event) => setOnlyFavorites(event.target.checked)}
          />
          Solo preferite (sessione)
        </label>
      </div>
      <div className="sw-atlas">
        <section aria-label="Catalogo metriche">
          <p className="sw-kicker">{visible.length} risultati</p>
          {visible.length === 0 ? (
            <p>Nessuna metrica corrispondente.</p>
          ) : (
            <div className="table-scroll">
              <table className="sw-metrics">
                <thead>
                  <tr>
                    <th scope="col">Metrica</th>
                    <th scope="col">Unità</th>
                    <th scope="col">Valore</th>
                    <th scope="col">Stato</th>
                    <th scope="col">Preferita</th>
                  </tr>
                </thead>
                <tbody>
                  {visible.map((metric) => {
                    const report = reports.get(metric.id)?.data;
                    const value = report?.current.status === "observed" ? report.current.value : null;
                    const favorite = favorites.includes(metric.id);
                    return (
                      <tr key={metric.id} data-selected={current?.id === metric.id}>
                        <th scope="row">
                          <button
                            type="button"
                            className="sw-metric-name"
                            aria-pressed={current?.id === metric.id}
                            onClick={() => setSelected(metric.id)}
                          >
                            {metric.label}
                          </button>
                          <small>{metricFamilies[metric.family]}</small>
                        </th>
                        <td>{metric.unit}</td>
                        <td className="sw-numeric">{value == null ? "—" : number.format(value)}</td>
                        <td>{lookup(metric)}</td>
                        <td>
                          <button
                            type="button"
                            className="sw-star"
                            aria-label={"Preferita: " + metric.label}
                            aria-pressed={favorite}
                            onClick={() =>
                              setFavorites((items) =>
                                favorite ? items.filter((id) => id !== metric.id) : [...items, metric.id],
                              )
                            }
                          >
                            {favorite ? "★" : "☆"}
                          </button>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          )}
        </section>
        {current && (
          <MetricInspector metric={current} report={reports.get(current.id)?.data} state={lookup(current)} />
        )}
      </div>
    </>
  );
}
