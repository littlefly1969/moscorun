import { useState } from "react";
import { AttestedRacePanel } from "../dashboard/AttestedRacePanel";
import { PacingScenarioPanel } from "../dashboard/PacingScenarioPanel";
import { PersonalAdaptationPanel } from "../dashboard/PersonalAdaptationPanel";
import { CalibratedTwinPanel } from "../workouts/CalibratedTwinPanel";
import { WorkoutPicker } from "./WorkoutPicker";

const scenarios = [
  { id: "pacing", title: "Pacing", hint: "Ripartizione del tempo sui segmenti" },
  { id: "gara", title: "Previsione gara", hint: "Calibrazione e intervalli empirici" },
  { id: "adattamento", title: "Fitness-fatigue", hint: "Risposta ai carichi individuali" },
  { id: "twin", title: "Digital Twin", hint: "Scenario cardiaco condizionale" },
];

export function SimulatorPage() {
  const [scenario, setScenario] = useState("pacing");
  return (
    <>
      <header className="sw-head">
        <p className="sw-kicker">04 / Scenari</p>
        <h1>Simulatore</h1>
        <p>Modelli condizionali, distinti dalle osservazioni e dalle prescrizioni.</p>
      </header>
      <div className="sw-tools" aria-label="Scenari disponibili">
        {scenarios.map((item) => (
          <button
            key={item.id}
            type="button"
            aria-pressed={scenario === item.id}
            onClick={() => {
              setScenario(item.id);
            }}
          >
            <strong>{item.title}</strong>
            <small>{item.hint}</small>
          </button>
        ))}
      </div>
      {scenario === "pacing" && <PacingScenarioPanel />}
      {scenario === "gara" && <AttestedRacePanel />}
      {scenario === "adattamento" && <PersonalAdaptationPanel />}
      {scenario === "twin" && (
        <WorkoutPicker>
          {(workout) => (
            <CalibratedTwinPanel key={`${workout.id}:${workout.revision}`} workoutId={workout.id} />
          )}
        </WorkoutPicker>
      )}
    </>
  );
}
