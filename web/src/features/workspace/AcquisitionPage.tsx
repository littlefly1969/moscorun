import { useInfiniteQuery, useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { Link, useSearchParams } from "react-router";
import { api, unwrap, type Schemas } from "../../api/client";
import { metricCatalog } from "./metricCatalog";

type Inventory = Schemas["ScientificInventory"];
type Cursor = { asOf: string; beforeTime: string; beforeId: string };
const number = new Intl.NumberFormat("it-IT", { maximumFractionDigits: 3 });
const sources = ["health-connect", "samsung-health", "garmin-connect", "athlete-feedback", "bilancia"];
const label = (id: string) => metricCatalog.find((metric) => metric.id === id)?.label ?? id;
const date = (value: string | null | undefined) => value ? new Date(value).toLocaleString("it-IT") : "—";

function ObservationHistory({ metric, source }: { metric: string; source: string }) {
  const history = useInfiniteQuery({
    queryKey: ["acquisition", "history", metric, source],
    initialPageParam: null as Cursor | null,
    queryFn: ({ pageParam }) =>
      unwrap(api.GET("/api/v1/science/inventory/observations", {
        params: { query: { metric, source, limit: 50, ...pageParam } },
      })),
    getNextPageParam: (page) => {
      const last = page.items.at(-1);
      return page.hasMore && last ? { asOf: page.asOf, beforeTime: last.time, beforeId: last.id } : undefined;
    },
  });
  const items = history.data?.pages.flatMap((page) => page.items) ?? [];
  return (
    <section className="card" aria-label="Storico delle osservazioni">
      <h2>Osservazioni memorizzate · {label(metric)}</h2>
      <p className="sw-muted">{source} · revisioni originali, non una serie ripulita o validata.</p>
      {history.isPending && <p role="status">Lettura dello storico…</p>}
      {history.isError && <p role="alert">Impossibile leggere lo storico.</p>}
      {history.isSuccess && items.length === 0 && <p>Nessuna osservazione per questa sorgente.</p>}
      {items.length > 0 && (
        <div className="table-scroll">
          <table>
            <thead>
              <tr>
                <th scope="col">Misura</th>
                <th scope="col">Valore</th>
                <th scope="col">Metodo e origine</th>
                <th scope="col">Disponibile dal</th>
                <th scope="col">Revisione</th>
                <th scope="col">Contesto</th>
              </tr>
            </thead>
            <tbody>
              {items.map((item) => (
                <tr key={item.id}>
                  <td>{date(item.time)}</td>
                  <td>{item.value == null ? "Mancante" : number.format(item.value) + " " + item.unit}</td>
                  <td>{item.method}<small className="sw-muted"> {item.providerAlgorithm ?? "Origine non dichiarata"}</small></td>
                  <td>{date(item.availableAt)}</td>
                  <td>{item.externalRevision ?? "—"}</td>
                  <td>{item.workoutId ? <Link to={"/allenamenti/" + item.workoutId}>Seduta</Link> : "Giornaliero / autonomo"}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      {history.hasNextPage && (
        <button type="button" disabled={history.isFetchingNextPage} onClick={() => {
          void history.fetchNextPage();
        }}>Altre osservazioni</button>
      )}
    </section>
  );
}

function InventoryView({ inventory }: { inventory: Inventory }) {
  const [params, setParams] = useSearchParams();
  const [source, setSource] = useState("all");
  const [search, setSearch] = useState("");
  const [presentOnly, setPresentOnly] = useState(false);
  const [history, setHistory] = useState<{ metric: string; source: string } | null>(null);
  const [selected, setSelected] = useState(params.get("metrica") ?? "");
  const allSources = Array.from(new Set([...sources, ...inventory.observations.map((row) => row.source),
    ...inventory.signals.map((row) => row.source)]));
  const ids = Array.from(new Set([...metricCatalog.map((metric) => metric.id),
    ...inventory.observations.map((row) => row.metric), ...inventory.signals.map((row) => row.metric)]));
  const observations = inventory.observations.filter((row) => source === "all" || row.source === source);
  const signals = inventory.signals.filter((row) => source === "all" || row.source === source);
  const scales = source === "all" || source === "bilancia" ? inventory.scales : [];
  const deviceName = (id: string | null) => id == null ? "Non registrato" :
    inventory.devices.find((device) => device.id === id)?.name ?? id;
  const rows = ids.map((id) => {
    const values = observations.filter((row) => row.metric === id);
    const chunks = signals.filter((row) => row.metric === id);
    const scaleRows = id === "weight" || id === "impedance" ? scales.reduce((sum, row) =>
      sum + (id === "weight" ? row.weighings : row.withImpedance), 0) : 0;
    const stored = values.reduce((sum, row) => sum + row.revisionRows, 0);
    const nonNull = values.reduce((sum, row) => sum + row.nonNullRows, 0);
    const chunkCount = chunks.reduce((sum, row) => sum + row.chunkRevisions, 0);
    const sampleCount = chunks.reduce((sum, row) => sum + row.storedSamples, 0);
    const last = [...values.map((row) => row.lastAvailableAt), ...chunks.map((row) => row.lastAvailableAt),
      ...(scaleRows > 0 ? scales.filter((row) => id === "weight" || row.withImpedance > 0).map((row) => row.lastReceivedAt) : [])]
      .sort((a, b) => Date.parse(b) - Date.parse(a))[0];
    return { id, values, chunks, scaleRows, stored, nonNull, chunkCount, sampleCount, last };
  });
  const present = (row: (typeof rows)[number]) => row.stored > 0 || row.chunkCount > 0 || row.scaleRows > 0;
  const normalized = search.trim().toLocaleLowerCase("it-IT");
  const visible = rows.filter((row) => (!presentOnly || present(row)) &&
    (label(row.id) + " " + row.id).toLocaleLowerCase("it-IT").includes(normalized));
  const current = rows.find((row) => row.id === selected);
  const daily = metricCatalog.find((metric) => metric.id === current?.id)?.access === "daily";
  return (
    <>
      <div className="sw-index">
        <span><strong>{rows.filter(present).length}</strong> metriche con record nell'archivio</span>
        <span><strong>{observations.reduce((sum, row) => sum + row.revisionRows, 0)}</strong> revisioni osservazioni</span>
        <span><strong>{signals.reduce((sum, row) => sum + row.chunkRevisions, 0)}</strong> revisioni chunk</span>
        <span><strong>{scales.reduce((sum, row) => sum + row.weighings, 0)}</strong> pesate</span>
      </div>
      <p className="sw-muted">Inventario al {date(inventory.asOf)}. I conteggi includono revisioni e non sono misure uniche.
        I chunk possono contenere campioni mancanti. Le pesate includono anche quelle non attribuite o escluse.
        Le serie native delle sedute restano in Allenamenti.</p>
      <div className="sw-controls">
        <label>Sorgente
          <select value={source} onChange={(event) => {
            setSource(event.target.value);
            setHistory(null);
          }}>
            <option value="all">Tutte le sorgenti</option>
            {allSources.map((item) => <option value={item} key={item}>{item}</option>)}
          </select>
        </label>
        <label>Cerca metrica
          <input type="search" value={search} onChange={(event) => { setSearch(event.target.value); }} />
        </label>
        <label className="sw-check">
          <input type="checkbox" checked={presentOnly} onChange={(event) => { setPresentOnly(event.target.checked); }} />
          Solo record presenti
        </label>
      </div>
      <p>Un conteggio zero significa solo che non ci sono record nella sezione di archivio interrogata,
        non che l'orologio non misuri il dato o che il telefono non abbia i permessi.</p>
      {current && (
        <section className="card" aria-label="Provenienza della metrica">
          <h2>{label(current.id)}</h2>
          <p>{daily ? "Analisi giornaliera prevista" : "Non esposta dal motore giornaliero"} ·
            la disponibilità per gli altri motori dipende dai loro protocolli.</p>
          {!present(current) && <p>Nessun record scientifico o pesata in questa selezione.</p>}
          {current.values.map((row, index) => (
            <div className="sw-acquisition-group" key={index}>
              <h3>{row.source} · {row.method}</h3>
              <p>{deviceName(row.deviceId)} · {row.session ? "Seduta" : "Autonomo"} · {row.unit}</p>
              <p>{row.nonNullRows}/{row.revisionRows} revisioni con valore · prima {date(row.firstTime)} · ultima {date(row.lastTime)}
                · disponibile dal server fino a {date(row.lastAvailableAt)}</p>
              <button type="button" onClick={() => { setHistory({ metric: current.id, source: row.source }); }}>Vedi osservazioni: {row.source}</button>
            </div>
          ))}
          {current.chunks.map((row, index) => (
            <div className="sw-acquisition-group" key={index}>
              <h3>{row.source} · segnale {row.method}</h3>
              <p>{deviceName(row.deviceId)} · {row.chunkRevisions} revisioni chunk · {row.storedSamples} campioni memorizzati</p>
              <p>Prima {date(row.firstTime)} · ultima {date(row.lastTime)} · ultima ricezione {date(row.lastAvailableAt)}</p>
            </div>
          ))}
          {current.scaleRows > 0 && <p>{current.scaleRows} record bilancia · <Link to="/forma">Verifica pesate e attribuzione</Link></p>}
        </section>
      )}
      {history && <ObservationHistory key={history.metric + "/" + history.source} metric={history.metric} source={history.source} />}
      <section aria-label="Inventario delle metriche">
        <div className="table-scroll">
          <table>
            <thead>
              <tr>
                <th scope="col">Metrica</th>
                <th scope="col">Stato archivio</th>
                <th scope="col">Valori / revisioni</th>
                <th scope="col">Chunk / campioni</th>
                <th scope="col">Pesate</th>
                <th scope="col">Ultima disponibilità</th>
              </tr>
            </thead>
            <tbody>
              {visible.map((row) => (
                <tr key={row.id} data-selected={selected === row.id}>
                  <th scope="row">
                    <button className="sw-metric-name" type="button" aria-pressed={selected === row.id} onClick={() => {
                      setSelected(row.id);
                      setParams({ metrica: row.id });
                      setHistory(null);
                    }}>{label(row.id)}</button>
                    <small className="sw-muted"> {row.id}</small>
                  </th>
                  <td>{row.nonNull > 0 || row.scaleRows > 0 ? "Valori presenti" : row.chunkCount > 0 ? "Chunk presenti" :
                    row.stored > 0 ? "Solo valori mancanti" : "Nessun record"}</td>
                  <td>{row.nonNull}/{row.stored}</td>
                  <td>{row.chunkCount}/{row.sampleCount}</td>
                  <td>{row.scaleRows}</td>
                  <td>{date(row.last)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>
      {visible.length === 0 && <p>Nessun risultato per i filtri selezionati.</p>}
      <section className="card" aria-label="Pesate per dispositivo">
        <h2>Bilancia · attribuzione</h2>
        {scales.length === 0 && <p>Nessuna pesata nella selezione.</p>}
        {scales.map((row, index) => <p key={index}>{deviceName(row.deviceId)} · {row.attribution} · {row.weighings} pesate,
          {row.withImpedance} con impedenza · ultima ricezione {date(row.lastReceivedAt)}</p>)}
        <Link to="/forma">Apri Forma</Link>
      </section>
      <section className="card" aria-label="Stato dei dispositivi">
        <h2>Dispositivi registrati</h2>
        <p>Ultimo contatto autenticato: non equivale a un dato ricevuto. Permessi e letture del telefono non sono ancora
          comunicati per singola metrica al server.</p>
        {inventory.devices.map((device) => <p key={device.id}>{device.name} · {device.revokedAt ? "Revocato" :
          "Ultimo contatto " + date(device.lastSeenAt)}</p>)}
        <Link to="/dispositivi">Gestisci dispositivi</Link>
      </section>
    </>
  );
}

export function AcquisitionPage() {
  const inventory = useQuery({
    queryKey: ["acquisition", "inventory"],
    queryFn: () => unwrap(api.GET("/api/v1/science/inventory")),
  });
  return (
    <>
      <header className="sw-head">
        <p className="sw-kicker">06 / Provenienza e diagnostica</p>
        <h1>Acquisizione</h1>
        <p>Verifica quali dati sono realmente memorizzati sul server, indipendentemente dalle analisi.</p>
      </header>
      <button type="button" disabled={inventory.isFetching} onClick={() => { void inventory.refetch(); }}>Aggiorna inventario</button>
      {inventory.isPending && <p role="status">Interrogazione dell'archivio…</p>}
      {inventory.isError && <p role="alert">Inventario non disponibile: non è possibile stabilire se i dati esistono.</p>}
      {inventory.data && !inventory.isError && <InventoryView inventory={inventory.data} />}
    </>
  );
}
