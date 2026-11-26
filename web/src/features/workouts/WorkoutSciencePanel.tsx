import { useWorkoutScience } from "./queries";

const labels = {
  HeartRate: "Frequenza cardiaca",
  Speed: "Velocità",
  Cadence: "Cadenza",
  Power: "Potenza",
} as const;

const number = new Intl.NumberFormat("it-IT", { maximumFractionDigits: 2 });
const percent = new Intl.NumberFormat("it-IT", { style: "percent", maximumFractionDigits: 1 });

export function WorkoutSciencePanel({ workoutId, revision }: { workoutId: string; revision: number }) {
  const report = useWorkoutScience(workoutId, revision);
  return (
    <section className="card" aria-labelledby="science-title">
      <h2 id="science-title">Analisi scientifica delle serie</h2>
      {report.isPending ? (
        <p role="status">Analisi in caricamento…</p>
      ) : report.isError ? (
        <p>Analisi non disponibile.</p>
      ) : (
        <>
          <p className="muted">
            Revisione {report.data.revision} · {report.data.methodVersion} · supporto massimo{" "}
            {report.data.maxGapSeconds} s. Le medie descrivono soltanto il supporto osservato.
          </p>
          <div className="table-scroll">
            <table>
              <thead>
                <tr>
                  <th scope="col">Metrica</th>
                  <th scope="col">Campioni</th>
                  <th scope="col">Copertura</th>
                  <th scope="col">Media temporale</th>
                </tr>
              </thead>
              <tbody>
                {report.data.channels.map((channel) => (
                  <tr key={channel.metric ?? "unknown"}>
                    <th scope="row">{channel.metric ? labels[channel.metric] : "Sconosciuta"}</th>
                    <td>{channel.sampleCount}</td>
                    <td>{percent.format(channel.coverage)}</td>
                    <td>
                      {channel.timeWeightedMean == null
                        ? channel.status === "missing"
                          ? "Non acquisita"
                          : "Supporto insufficiente"
                        : `${number.format(channel.timeWeightedMean)} ${channel.unit}`}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </>
      )}
    </section>
  );
}
