import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState, type SubmitEvent } from "react";
import { ApiError, api, unwrap, type Schemas } from "../../api/client";

type Feedback = Schemas["ScienceFeedbackReport"];
type Request = Schemas["ScienceFeedbackRequest"];

const labels: Record<string, string> = {
  rpe: "Sforzo percepito RPE",
  fatigue: "Fatica",
  stressPerceived: "Stress percepito",
  soreness: "Indolenzimento",
  pain: "Dolore",
  sleepQuality: "Qualità del sonno",
  hunger: "Fame",
  gastrointestinalDiscomfort: "Disturbi gastrointestinali",
  motivation: "Motivazione",
  sleepDuration: "Durata sonno",
  sleepBedtimeMinute: "Ora locale a letto (minuti da mezzanotte, notte conclusa nel giorno UTC)",
  sleepWakeMinute: "Ora locale risveglio (minuti da mezzanotte)",
  sleepInBedDuration: "Durata effettiva a letto, senza dedurla dagli orari",
  sleepNightConfirmed: "Notte principale, stesso fuso abituale (1 sì, 0 no)",
  napDuration: "Durata riposo diurno",
  energyIntake: "Energia alimentare",
  carbohydrateIntake: "Carboidrati",
  proteinIntake: "Proteine",
  fatIntake: "Grassi",
  fluidIntake: "Liquidi",
  sodiumIntake: "Sodio",
  caffeineIntake: "Caffeina",
  trainingDayComplete: "Diario allenamenti completo (1 sì, 0 no; dopo chiusura UTC)",
  nutritionDiaryComplete: "Totali nutrizionali completi (1 sì, 0 no)",
  restingEnergyExpenditure: "Dispendio a riposo dichiarato",
  thermicEffectOfFood: "Termogenesi alimentare",
  nonExerciseEnergyExpenditureNet: "Attività non sportiva netta",
  exerciseEnergyExpenditureNet: "Esercizio netto",
  fatFreeMass: "Massa magra dichiarata del giorno",
  preExerciseWeight: "Peso prima della seduta",
  postExerciseWeight: "Peso dopo la seduta",
  exerciseUrineVolume: "Urina durante la seduta",
  sweatProtocolConfirmed: "Bilancio di massa controllato (1 sì, 0 no)",
  maximalFlatRun: "Prova massimale su percorso piano misurato, senza pause (1 sì, 0 no)",
  comparableRunningPower: "Potenza di corsa con stesso sensore e algoritmo delle altre prove (1 sì, 0 no)",
};

const number = new Intl.NumberFormat("it-IT", { maximumFractionDigits: 2 });

function NutritionSummary({
  report,
  session,
}: {
  report: Schemas["NutritionFeedbackReport"];
  session: boolean;
}) {
  const display = (value: number | null) => (value == null ? "—" : number.format(value));
  return (
    <section aria-label="Analisi nutrizionale">
      <h3>Contabilità nutrizionale dichiarata</h3>
      <p>{report.diaryComplete ? "Diario completo dichiarato" : "Diario non confermato completo"}</p>
      <p>Stime descrittive, non obiettivi dietetici o diagnosi.</p>
      {session ? (
        <dl>
          <dt>Carboidrati (g/h)</dt>
          <dd>{display(report.carbohydrateGramsPerHour)}</dd>
          <dt>Liquidi (mL/h)</dt>
          <dd>{display(report.fluidMillilitersPerHour)}</dd>
          <dt>Sodio (mg/h)</dt>
          <dd>{display(report.sodiumMilligramsPerHour)}</dd>
          <dt>Sudorazione stimata (L/h)</dt>
          <dd>{display(report.estimatedSweatLitersPerHour)}</dd>
        </dl>
      ) : (
        <dl>
          <dt>Dispendio totale (kcal)</dt>
          <dd>{display(report.totalExpenditureKcal)}</dd>
          <dt>Bilancio energetico (kcal)</dt>
          <dd>{display(report.energyBalanceKcal)}</dd>
          <dt>Disponibilità energetica (kcal/kg massa magra)</dt>
          <dd>{display(report.energyAvailabilityKcalPerKg)}</dd>
        </dl>
      )}
    </section>
  );
}

function FeedbackForm({
  report,
  onSaved,
  onRefresh,
}: {
  report: Feedback;
  onSaved: (value: Feedback) => void;
  onRefresh: () => void;
}) {
  const save = useMutation({
    mutationFn: (body: Request) => unwrap(api.PUT("/api/v1/science/feedback", { body })),
    onSuccess: onSaved,
  });
  const submit = (event: SubmitEvent<HTMLFormElement>) => {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    const note = form.get("note");
    save.mutate({
      day: report.day,
      workoutId: report.workoutId,
      workoutRevision: report.workoutRevision,
      revision: (report.revision ?? 0) + 1,
      values: report.values.map(({ metric }) => {
        const value = form.get(metric);
        return { metric, value: typeof value === "string" && value !== "" ? Number(value) : null };
      }),
      note: typeof note === "string" && note !== "" ? note : null,
    });
  };

  return (
    <>
      <p>
        {report.revision == null ? "Nessun feedback registrato." : `Revisione salvata: ${report.revision}.`}
      </p>
      {report.workoutId != null && (
        <p>
          Carico session-RPE:{" "}
          <strong>
            {report.sessionRpeLoad == null ? "Non disponibile" : number.format(report.sessionRpeLoad)}
          </strong>{" "}
          {report.sessionRpeLoad != null && "UA"} · durata attiva × RPE, senza imputazione delle pause.
        </p>
      )}
      <NutritionSummary report={report.nutrition} session={report.workoutId != null} />
      <form className="inline-form" onSubmit={submit}>
        {report.values.map(({ metric, unit, value }) => (
          <label key={metric}>
            {labels[metric] ?? metric} ({unit})
            <input
              name={metric}
              type="number"
              min={0}
              max={unit === "1" ? 1 : unit === "0-10" ? 10 : unit === "s" ? 86400 : unit === "min" ? 1439 : undefined}
              step={unit === "1" || unit === "min" ? 1 : "any"}
              defaultValue={value ?? ""}
            />
          </label>
        ))}
        <label>
          Note
          <textarea name="note" maxLength={4000} defaultValue={report.note ?? ""} />
        </label>
        <button type="submit" disabled={save.isPending}>
          Salva feedback
        </button>
        {save.isError && (
          <p role="alert">
            {save.error instanceof ApiError && save.error.status === 409
              ? "Revisione cambiata: ricarica il feedback prima di riprovare."
              : "Salvataggio non riuscito."}
          </p>
        )}
        {save.isError && (
          <button type="button" onClick={onRefresh}>
            Ricarica feedback
          </button>
        )}
      </form>
    </>
  );
}

export function ScienceFeedbackPanel({
  workoutId,
  workoutRevision,
}: {
  workoutId?: string;
  workoutRevision?: number;
}) {
  const [today] = useState(() => new Date().toISOString().slice(0, 10));
  const [day, setDay] = useState(today);
  const client = useQueryClient();
  const queryKey = ["science-feedback", workoutId ?? day, workoutRevision ?? null] as const;
  const feedback = useQuery({
    queryKey,
    queryFn: () =>
      unwrap(api.GET("/api/v1/science/feedback", { params: { query: workoutId ? { workoutId } : { day } } })),
    enabled: workoutId != null || day !== "",
  });

  return (
    <section className="card" aria-labelledby="feedback-title">
      <h2 id="feedback-title">{workoutId ? "Feedback della seduta" : "Diario giornaliero"}</h2>
      <p>
        Inserisci solo dati osservati o dichiarati. Lascia vuoti i valori non noti; zero è una misura valida.
      </p>
      {!workoutId && (
        <label>
          Giorno del diario (UTC)
          <input
            type="date"
            value={day}
            max={today}
            onChange={(event) => {
              setDay(event.target.value);
            }}
          />
        </label>
      )}
      {feedback.isPending && <p role="status">Caricamento feedback…</p>}
      {feedback.isError && <p role="alert">Feedback non disponibile.</p>}
      {feedback.data?.status === "ambiguous" && (
        <p role="alert">Revisioni ambigue: modifica non consentita.</p>
      )}
      {feedback.data && feedback.data.status !== "ambiguous" && (
        <FeedbackForm
          key={`${workoutId ?? day}:${feedback.data.workoutRevision}:${feedback.data.revision}`}
          report={feedback.data}
          onSaved={(value) => client.setQueryData(queryKey, value)}
          onRefresh={() => {
            void feedback.refetch();
          }}
        />
      )}
    </section>
  );
}
