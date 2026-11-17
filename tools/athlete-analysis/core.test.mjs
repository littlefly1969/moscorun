import assert from "node:assert/strict";
import { describe, test } from "node:test";
import {
  athleteHoldout,
  auditSeries,
  buildContext,
  causalAlign,
  chronologicalSplit,
  persistenceReplay,
  validateFeedback,
  validateObservation,
} from "./core.mjs";

const observation = {
  ownerId: "a",
  metric: "weight",
  unit: "kg",
  time: "2026-01-01T00:00:00Z",
  availableAt: "2026-01-01T00:00:00Z",
  source: "scale",
  method: "measured",
  value: 70,
};
const feedback = {
  ownerId: "a",
  id: "f1",
  time: "2026-01-01T01:00:00Z",
  availableAt: "2026-01-01T01:01:00Z",
  scale: "moscorun-feedback-v1",
  rpe: 7,
  pain: 0,
  notes: "untrusted content",
};
const workout = {
  ownerId: "a",
  id: "w1",
  revision: 1,
  source: "synthetic",
  startedAt: "2026-01-01T00:00:00Z",
  endedAt: "2026-01-01T00:00:01Z",
  availableAt: "2026-01-01T00:00:02Z",
  series: [
    {
      metric: "heartRate",
      unit: "bpm",
      availableAt: "2026-01-01T00:00:02Z",
      samples: [
        { time: "2026-01-01T00:00:00Z", value: 100 },
        { time: "2026-01-01T00:00:01Z", value: 110 },
      ],
    },
  ],
};
const dataset = {
  schemaVersion: 1,
  ownerId: "a",
  workouts: [workout],
  observations: [observation],
  feedback: [feedback],
};
const weight = (time, value, id) => ({ ...observation, id, time, availableAt: time, value });

describe("auditSeries", () => {
  test("media pesata nel tempo con supporto limitato nei buchi", () => {
    const r = auditSeries(
      [
        { time: 0, value: 100 },
        { time: 1000, value: 110 },
        { time: 11000, value: 120 },
      ],
      { start: 0, end: 11000, maxGapMs: 2000 },
    );
    assert.equal(r.timeWeightedMean, (100 * 1000 + 110 * 2000) / 3000);
    assert.equal(r.maxIntervalMs, 10000);
    assert.equal(r.medianIntervalMs, 5500);
  });
  test("il campione finale non pesa sulla media", () => {
    assert.equal(
      auditSeries(
        [
          { time: 0, value: 0 },
          { time: 1000, value: 2 },
        ],
        { start: 0, end: 1000 },
      ).timeWeightedMean,
      0,
    );
  });
  test("duplicati discordanti esclusi, concordi conteggiati", () => {
    const r = auditSeries([
      { time: 0, value: 1 },
      { time: 0, value: 2 },
      { time: 5, value: 3 },
      { time: 5, value: 3 },
    ]);
    assert.equal(r.validCount, 1);
    assert.equal(r.duplicateCount, 2);
    assert.equal(r.conflictingDuplicateCount, 1);
  });
  test("valori non validi e fuori finestra", () => {
    assert.equal(
      auditSeries([
        { time: 0, value: null },
        { time: 1, value: NaN },
      ]).invalidCount,
      2,
    );
    assert.equal(auditSeries([{ time: 10, value: 1 }], { start: 0, end: 5 }).outsideCount, 1);
  });
  test("serie con un solo campione: copertura nulla e statistiche coerenti", () => {
    const r = auditSeries([{ time: 0, value: 1 }]);
    assert.equal(r.coverage, 0);
    assert.equal(r.timeWeightedMean, null);
    assert.equal(r.minimum, 1);
    assert.equal(r.maximum, 1);
  });
  test("serie vuota", () => {
    const r = auditSeries([]);
    assert.equal(r.minimum, null);
    assert.equal(r.medianIntervalMs, null);
    assert.equal(r.maxIntervalMs, null);
  });
  test("valori ripetuti contati", () => {
    assert.equal(
      auditSeries([
        { time: 0, value: 1 },
        { time: 1, value: 1 },
      ]).repeatedValueCount,
      1,
    );
  });
  test("parametri non validi rifiutati", () => {
    assert.throws(() => auditSeries("x"), /invalid_series/);
    assert.throws(() => auditSeries([], { maxGapMs: 0 }), /invalid_series/);
    assert.throws(() => auditSeries([], { start: 10, end: 0 }), /invalid_window/);
    assert.throws(() => auditSeries([], { start: "non una data" }), /invalid_window/);
  });
});

describe("causalAlign", () => {
  test("nessun valore dal futuro", () => {
    assert.equal(causalAlign([{ time: 1000, value: 10 }], [999])[0].value, null);
  });
  test("valore troppo vecchio scartato", () => {
    assert.equal(causalAlign([{ time: 1000, value: 10 }], [11000], { maxAgeMs: 2000 })[0].value, null);
  });
  test("duplicati discordanti non usati", () => {
    const samples = [
      { time: 1000, value: 10 },
      { time: 1000, value: 11 },
      { time: 1000, value: 12 },
    ];
    assert.equal(causalAlign(samples, [1000])[0].value, null);
  });
  test("ricerca binaria su serie lunghe ed età riportata", () => {
    const samples = Array.from({ length: 10000 }, (_, i) => ({ time: i * 1000, value: i }));
    assert.deepEqual(causalAlign(samples, [9999500])[0], {
      time: 9999500,
      value: 9999,
      observed: true,
      ageMs: 500,
    });
  });
  test("campioni non validi ignorati e parametri controllati", () => {
    assert.equal(causalAlign([{ time: "x", value: 1 }], [0])[0].observed, false);
    assert.throws(() => causalAlign([], [0], { maxAgeMs: -1 }), /invalid_max_age/);
    assert.throws(() => causalAlign([], ["x"]), /invalid_target_time/);
  });
});

describe("validateObservation", () => {
  test("osservazione completa accettata", () => assert.equal(validateObservation(observation, "a"), true));
  test("owner diverso rifiutato", () => assert.throws(() => validateObservation(observation, "b"), /owner/));
  test("unità diversa dal catalogo rifiutata", () =>
    assert.throws(() => validateObservation({ ...observation, unit: "lb" }, "a"), /unit_mismatch/));
  test("stima del fornitore senza algoritmo rifiutata", () =>
    assert.throws(
      () => validateObservation({ ...observation, method: "provider_estimated" }, "a"),
      /provider_algorithm_required/,
    ));
  test("derivato senza versione o input rifiutato", () =>
    assert.throws(
      () => validateObservation({ ...observation, method: "derived" }, "a"),
      /derivation_missing/,
    ));
  test("metodo, valore e metadati controllati", () => {
    assert.throws(
      () => validateObservation({ ...observation, method: "guess" }, "a"),
      /invalid_observation_method/,
    );
    assert.throws(
      () => validateObservation({ ...observation, value: "70" }, "a"),
      /invalid_observation_value/,
    );
    assert.throws(() => validateObservation({ ...observation, source: "" }, "a"), /metadata_missing/);
    assert.equal(validateObservation({ ...observation, value: null }, "a"), true);
  });
});

describe("validateFeedback", () => {
  test("feedback completo accettato", () => assert.equal(validateFeedback(feedback, "a"), true));
  test("scala fuori intervallo rifiutata", () =>
    assert.throws(() => validateFeedback({ ...feedback, rpe: 11 }, "a"), /out_of_range/));
  test("dolore senza localizzazione rifiutato", () =>
    assert.throws(() => validateFeedback({ ...feedback, pain: 3 }, "a"), /pain_location_required/));
  test("metadati, owner e note controllati", () => {
    assert.throws(() => validateFeedback(feedback, "b"), /owner_mismatch/);
    assert.throws(() => validateFeedback({ ...feedback, scale: "v0" }, "a"), /metadata_missing/);
    assert.throws(
      () => validateFeedback({ ...feedback, notes: "x".repeat(2001) }, "a"),
      /invalid_feedback_notes/,
    );
  });
});

describe("buildContext", () => {
  const ctx = buildContext(dataset, { ownerId: "a", asOf: "2026-01-01T00:00:03Z" });
  test("include solo ciò che era disponibile ad asOf", () => {
    assert.equal(ctx.workouts.length, 1);
    assert.equal(ctx.feedback.length, 0);
    assert.equal(buildContext(dataset, { ownerId: "a", asOf: "2026-01-01T00:00:01Z" }).workouts.length, 0);
  });
  test("record di un altro owner rifiutati", () =>
    assert.throws(
      () =>
        buildContext(
          { ...dataset, observations: [{ ...observation, ownerId: "b" }] },
          { ownerId: "a", asOf: "2026-01-02" },
        ),
      /owner_mismatch/,
    ));
  test("richieste non valide rifiutate", () => {
    assert.throws(() => buildContext(dataset, { ownerId: "b", asOf: "2026-01-02" }), /owner_mismatch/);
    assert.throws(() => buildContext(dataset, { ownerId: "a", asOf: "x" }), /invalid_context_request/);
    assert.throws(
      () => buildContext(dataset, { ownerId: "a", asOf: "2026-01-02", maxWorkouts: 101 }),
      /invalid_context_request/,
    );
  });
  test("impronta deterministica", () =>
    assert.equal(
      buildContext(dataset, { ownerId: "a", asOf: "2026-01-01T00:00:03Z" }).contentSha256,
      ctx.contentSha256,
    ));
  test("nessun GPS né campi privati nel contesto", () => {
    const priv = buildContext(
      { ...dataset, observations: [{ ...observation, latitude: 42, privateToken: "not-for-context" }] },
      { ownerId: "a", asOf: "2026-01-02" },
    );
    const text = JSON.stringify(priv);
    assert.equal(text.includes("privateToken"), false);
    assert.equal(text.includes("latitude"), false);
    const coords = buildContext(
      {
        ...dataset,
        workouts: [
          {
            ...workout,
            series: [...workout.series, { ...workout.series[0], metric: "latitude", unit: "deg" }],
          },
        ],
      },
      { ownerId: "a", asOf: "2026-01-02" },
    );
    assert.equal(coords.workouts[0].series.length, 1);
  });
  test("troncamento: tiene le osservazioni più recenti anche se l'ingresso non è ordinato", () => {
    const ds = {
      ...dataset,
      observations: [weight("2026-03-01T00:00:00Z", 72, "o2"), weight("2026-01-01T00:00:00Z", 70, "o1")],
    };
    const r = buildContext(ds, { ownerId: "a", asOf: "2026-04-01", maxObservations: 1 });
    assert.deepEqual(
      r.observations.map((o) => o.value),
      [72],
    );
    assert.equal(r.completeness.eligibleObservations, 2);
  });
  test("troncamento: tiene il feedback più recente anche se l'ingresso non è ordinato", () => {
    const later = {
      ...feedback,
      id: "f2",
      time: "2026-02-01T00:00:00Z",
      availableAt: "2026-02-01T00:00:00Z",
    };
    const r = buildContext(
      { ...dataset, feedback: [later, feedback] },
      { ownerId: "a", asOf: "2026-04-01", maxFeedback: 1 },
    );
    assert.deepEqual(
      r.feedback.map((f) => f.id),
      ["f2"],
    );
  });
  test("sedute dalla più recente e troncate", () => {
    const w2 = { ...workout, id: "w2", startedAt: "2026-01-02T00:00:00Z", endedAt: "2026-01-02T00:00:01Z" };
    const r = buildContext(
      { ...dataset, workouts: [workout, w2] },
      { ownerId: "a", asOf: "2026-02-01", maxWorkouts: 1 },
    );
    assert.deepEqual(
      r.workouts.map((w) => w.id),
      ["w2"],
    );
    assert.equal(r.completeness.truncated, true);
  });
  test("impronta indipendente dall'ordine di ingresso", () => {
    const w2 = { ...workout, id: "w2", startedAt: "2026-01-02T00:00:00Z", endedAt: "2026-01-02T00:00:01Z" };
    const obs = [
      weight("2026-01-01T00:00:00Z", 70, "o1"),
      weight("2026-02-01T00:00:00Z", 71, "o2"),
      weight("2026-02-01T00:00:00Z", 71.5, "o3"),
    ];
    const base = { ...dataset, workouts: [workout, w2], observations: obs };
    const reversed = { ...dataset, workouts: [w2, workout], observations: [...obs].reverse() };
    const opts = { ownerId: "a", asOf: "2026-04-01" };
    assert.equal(buildContext(base, opts).contentSha256, buildContext(reversed, opts).contentSha256);
  });
  test("osservazioni fuori catalogo contate ma escluse", () => {
    const r = buildContext(
      { ...dataset, observations: [{ ...observation, metric: "custom:x", unit: "u" }] },
      { ownerId: "a", asOf: "2026-04-01" },
    );
    assert.equal(r.observations.length, 0);
    assert.equal(r.completeness.unmappedObservations, 1);
  });
});

describe("split e baseline", () => {
  test("split cronologico", () => {
    const split = chronologicalSplit([workout, { ...workout, id: "w2", startedAt: "2026-01-02T00:00:00Z" }]);
    assert.deepEqual(
      split.test.map((w) => w.id),
      ["w2"],
    );
    assert.deepEqual(chronologicalSplit([workout]), { train: [workout], test: [] });
  });
  test("split cronologico: duplicati, atleti multipli e parametri rifiutati", () => {
    assert.throws(() => chronologicalSplit([workout, workout]), /duplicate_workout_id/);
    assert.throws(
      () => chronologicalSplit([workout, { ...workout, id: "w2", ownerId: "b" }]),
      /athlete_holdout/,
    );
    assert.throws(() => chronologicalSplit([workout], { testFraction: 1 }), /invalid_split/);
    assert.throws(() => chronologicalSplit([{ ...workout, id: "" }]), /invalid_workout/);
  });
  test("holdout per atleta", () => {
    assert.equal(athleteHoldout([workout, { ...workout, id: "w2", ownerId: "b" }], "b").train.length, 1);
    assert.throws(() => athleteHoldout([{ ...workout, ownerId: "" }], "b"), /invalid_athlete_split/);
  });
  test("baseline di persistenza", () => {
    const series = [
      { time: 0, value: 100 },
      { time: 1000, value: 110 },
      { time: 2000, value: 120 },
    ];
    const r = persistenceReplay(series, { horizonMs: 1000 });
    assert.equal(r.mae, 10);
    assert.equal(r.rmse, 10);
    assert.equal(r.excluded, 1);
    assert.equal(persistenceReplay([], { horizonMs: 1000 }).mae, null);
    assert.throws(() => persistenceReplay(series, { horizonMs: 0 }), /invalid_horizon/);
  });
});
