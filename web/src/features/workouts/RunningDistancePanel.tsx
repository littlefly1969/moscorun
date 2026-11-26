import { useMutation } from "@tanstack/react-query";
import { api, unwrap, type Schemas } from "../../api/client";

const number = new Intl.NumberFormat("it-IT", { maximumFractionDigits: 1 });
const format = (value: number | null) => (value == null ? "—" : number.format(value));

function Results({ report }: { report: Schemas["RunningDistanceReport"] }) {
  return (
    <>
      <p>Stato: {report.status}</p>
      <p>Giorni precedenti completi: {report.completeDays}/30</p>
      <dl>
        <dt>Distanza seduta (m)</dt>
        <dd>{format(report.current.distanceMeters)}</dd>
        <dt>Seduta precedente più lunga (m)</dt>
        <dd>{format(report.priorLongestMeters)}</dd>
        <dt>Variazione distanza (%)</dt>
        <dd>{format(report.increasePercent)}</dd>
      </dl>
      <div className="table-scroll">
        <table>
          <thead>
            <tr>
              <th scope="col">Giorno UTC</th>
              <th scope="col">Stato</th>
              <th scope="col">Totale (m)</th>
              <th scope="col">Massimo (m)</th>
            </tr>
          </thead>
          <tbody>
            {report.days.map((day) => (
              <tr key={day.day}>
                <th scope="row">{day.day}</th>
                <td>{day.status}</td>
                <td>{format(day.totalMeters)}</td>
                <td>{format(day.longestMeters)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </>
  );
}

export function RunningDistancePanel({ workoutId }: { workoutId: string }) {
  const analysis = useMutation({
    mutationFn: () =>
      unwrap(api.GET("/api/v1/workouts/{id}/science/distance-load", { params: { path: { id: workoutId } } })),
  });
  return (
    <section className="card" aria-labelledby="distance-load-title">
      <h2 id="distance-load-title">Variazione della distanza di corsa</h2>
      <p>Confronto descrittivo con il massimo dei 30 giorni UTC conclusi precedenti alla seduta.</p>
      <p>Richiede storico completo attestato e disponibile prima della seduta. Non è rischio infortunio.</p>
      <button
        type="button"
        disabled={analysis.isPending}
        onClick={() => {
          analysis.mutate();
        }}
      >
        Confronta distanza
      </button>
      {analysis.isPending && <p role="status">Analisi in corso…</p>}
      {analysis.isError && <p role="alert">Confronto non disponibile.</p>}
      {analysis.data && <Results report={analysis.data} />}
    </section>
  );
}
