import { Link } from "react-router";
import {
  activityLabel,
  formatBpm,
  formatDateTime,
  formatDistance,
  formatDuration,
  formatPace,
} from "../../lib/format";
import { useWorkouts } from "./queries";

export function WorkoutsPage() {
  const workouts = useWorkouts();
  if (workouts.isPending) return <p role="status">Caricamento…</p>;
  if (workouts.isError) return <p role="alert">Impossibile caricare gli allenamenti.</p>;
  const items = workouts.data.items;

  return (
    <>
      <h1>Allenamenti</h1>
      <section className="card" aria-labelledby="workouts-title">
        <h2 id="workouts-title">Ultimi 90 giorni</h2>
        {items.length === 0 ? (
          <p className="muted">
            Nessun allenamento. Dall&apos;app Android: Configura server, poi Invia allenamenti al server.
          </p>
        ) : (
          <div className="table-scroll">
            <table>
              <thead>
                <tr>
                  <th scope="col">Data</th>
                  <th scope="col">Attività</th>
                  <th scope="col" className="num">
                    Durata
                  </th>
                  <th scope="col" className="num">
                    Distanza
                  </th>
                  <th scope="col" className="num">
                    Passo
                  </th>
                  <th scope="col" className="num">
                    FC media
                  </th>
                  <th scope="col">GPS</th>
                </tr>
              </thead>
              <tbody>
                {items.map((w) => {
                  const seconds =
                    w.activeDurationSeconds ?? (Date.parse(w.endedAt) - Date.parse(w.startedAt)) / 1000;
                  return (
                    <tr key={w.id}>
                      <td>
                        <Link to={`/allenamenti/${w.id}`}>{formatDateTime(w.startedAt)}</Link>
                      </td>
                      <td>{activityLabel(w.activityType)}</td>
                      <td className="num">{formatDuration(seconds)}</td>
                      <td className="num">{formatDistance(w.distanceM)}</td>
                      <td className="num">{formatPace(seconds, w.distanceM)}</td>
                      <td className="num">{formatBpm(w.averageHeartRate)}</td>
                      <td>{w.trackPointCount > 0 ? "Sì" : "No"}</td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </>
  );
}
