import { Link, useSearchParams } from "react-router";
import { DailySessionLoadPanel } from "../dashboard/DailySessionLoadPanel";
import { PersonalResearchPanel } from "../dashboard/PersonalResearchPanel";
import { RrChunkPanel } from "../dashboard/RrChunkPanel";
import { SleepDiaryPanel } from "../dashboard/SleepDiaryPanel";
import { SleepPerformancePanel } from "../dashboard/SleepPerformancePanel";
import { ScienceFeedbackPanel } from "../science/ScienceFeedbackPanel";
import { WorkoutPicker } from "./WorkoutPicker";
import { WorkoutSciencePanel } from "../workouts/WorkoutSciencePanel";
import { WorkoutDurabilityPanel } from "../workouts/WorkoutDurabilityPanel";
import { WorkoutIntensityPanel } from "../workouts/WorkoutIntensityPanel";
import { CardiacRecoveryPanel } from "../workouts/CardiacRecoveryPanel";
import { WorkoutReservePanel } from "../workouts/WorkoutReservePanel";
import { RunningStepPanel } from "../workouts/RunningStepPanel";
import { ExtendedBiomechanicsPanel } from "../workouts/ExtendedBiomechanicsPanel";
import { WorkoutEnvironmentPanel } from "../workouts/WorkoutEnvironmentPanel";
import { RunningDistancePanel } from "../workouts/RunningDistancePanel";
import type { WorkoutSummary } from "../../api/client";

const methods = [
  ["ricerca", "Ricerca personale", "Benchmark temporale e ablation"],
  ["carico", "Carico longitudinale", "Session-RPE, monotonia e strain"],
  ["rr", "HRV e intervalli RR", "Selezione delle serie e protocollo"],
  ["sonno", "Regolarità del sonno", "Diario notturno e qualità"],
  ["diario", "Feedback e nutrizione", "Osservazioni e attestazioni"],
  ["seduta", "Analisi della seduta", "Durability, biomeccanica, intensità e contesto"],
] as const;

function WorkoutMethods({ workout }: { workout: WorkoutSummary }) {
  const id = workout.id;
  return (
    <>
      <p>
        <Link to={"/allenamenti/" + id}>Apri seduta completa</Link> · revisione {workout.revision}
      </p>
      <WorkoutSciencePanel workoutId={id} revision={workout.revision} />
      <ScienceFeedbackPanel workoutId={id} workoutRevision={workout.revision} />
      <WorkoutIntensityPanel workoutId={id} />
      <CardiacRecoveryPanel workoutId={id} />
      <WorkoutReservePanel workoutId={id} />
      {workout.activityType.toUpperCase() === "RUNNING" && (
        <>
          <RunningStepPanel workoutId={id} />
          <ExtendedBiomechanicsPanel workoutId={id} />
          <WorkoutEnvironmentPanel workoutId={id} />
          <RunningDistancePanel workoutId={id} />
        </>
      )}
      <WorkoutDurabilityPanel
        workoutId={id}
        duration={(Date.parse(workout.endedAt) - Date.parse(workout.startedAt)) / 1000}
      />
    </>
  );
}

export function LaboratoryPage() {
  const [params, setParams] = useSearchParams();
  const requested = params.get("metodo");
  const method = methods.find(([id]) => id === requested)?.[0] ?? "ricerca";
  return (
    <>
      <header className="sw-head">
        <p className="sw-kicker">03 / Metodo sperimentale</p>
        <h1>Laboratorio</h1>
        <p>Seleziona un protocollo reale. Il motore calcola; l’interfaccia conserva condizioni e limiti.</p>
      </header>
      <div className="sw-tools" aria-label="Metodi disponibili">
        {methods.map(([id, title, hint]) => (
          <button
            key={id}
            type="button"
            aria-pressed={method === id}
            onClick={() => {
              setParams({ metodo: id });
            }}
          >
            <strong>{title}</strong>
            <small>{hint}</small>
          </button>
        ))}
      </div>
      {method === "ricerca" && <PersonalResearchPanel />}
      {method === "carico" && <DailySessionLoadPanel />}
      {method === "rr" && <RrChunkPanel />}
      {method === "sonno" && <SleepDiaryPanel />}
      {method === "diario" && <ScienceFeedbackPanel />}
      {method === "seduta" && (
        <WorkoutPicker>
          {(workout) => <WorkoutMethods key={`${workout.id}:${workout.revision}`} workout={workout} />}
        </WorkoutPicker>
      )}
      <p>
        <Link to="/evidenze">Associazioni sonno e prestazione →</Link>
      </p>
    </>
  );
}

export function EvidencePage() {
  return (
    <>
      <header className="sw-head">
        <p className="sw-kicker">05 / Evidenza individuale</p>
        <h1>Mappa delle evidenze</h1>
        <p>
          Nessuna connessione a priori. Le relazioni compaiono soltanto quando il motore restituisce un
          risultato statistico sul protocollo selezionato.
        </p>
      </header>
      <SleepPerformancePanel showMap />
    </>
  );
}
