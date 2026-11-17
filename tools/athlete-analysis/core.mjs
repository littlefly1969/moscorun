import { createHash } from "node:crypto";
import { readFileSync } from "node:fs";
const catalog = JSON.parse(readFileSync(new URL("./catalog.json", import.meta.url), "utf8"));
const units = new Map(catalog.metrics.map((m) => [m.metric, m.unit]));
const finite = (n) => typeof n === "number" && Number.isFinite(n);
const stamp = (v) => (typeof v === "number" ? v : Date.parse(v));
const fail = (code) => {
  throw new Error(code);
};
const median = (a) => {
  const s = [...a].sort((x, y) => x - y);
  return s.length
    ? s.length % 2
      ? s[(s.length - 1) / 2]
      : (s[s.length / 2 - 1] + s[s.length / 2]) / 2
    : null;
};
export function auditSeries(samples, { start, end, maxGapMs = 5000 } = {}) {
  if (!Array.isArray(samples) || !finite(maxGapMs) || maxGapMs <= 0) fail("invalid_series");
  const from = start === undefined ? -Infinity : stamp(start),
    until = end === undefined ? Infinity : stamp(end);
  if (Number.isNaN(from) || Number.isNaN(until) || from > until) fail("invalid_window");
  let invalid = 0,
    outside = 0,
    duplicates = 0,
    conflicts = 0;
  const slots = new Map();
  for (const sample of samples) {
    const t = stamp(sample.time);
    if (!finite(t) || !finite(sample.value)) {
      invalid++;
      continue;
    }
    if (t < from || t > until) {
      outside++;
      continue;
    }
    if (slots.has(t)) {
      duplicates++;
      if (slots.get(t) !== sample.value) {
        conflicts++;
        slots.set(t, null);
      }
    } else slots.set(t, sample.value);
  }
  const valid = [...slots].filter(([, v]) => v !== null).sort((a, b) => a[0] - b[0]);
  const gaps = valid.slice(1).map((s, i) => s[0] - valid[i][0]);
  let supportedMs = 0,
    weighted = 0;
  for (let i = 0; i + 1 < valid.length; i++) {
    const dt = Math.min(valid[i + 1][0] - valid[i][0], maxGapMs);
    supportedMs += dt;
    weighted += valid[i][1] * dt;
  }
  const extent =
    finite(from) && finite(until) ? until - from : valid.length > 1 ? valid.at(-1)[0] - valid[0][0] : 0;
  return {
    sampleCount: samples.length,
    validCount: valid.length,
    invalidCount: invalid,
    outsideCount: outside,
    duplicateCount: duplicates,
    conflictingDuplicateCount: conflicts,
    medianIntervalMs: median(gaps),
    maxIntervalMs: gaps.length ? gaps.reduce((a, b) => Math.max(a, b), 0) : null,
    repeatedValueCount: valid.slice(1).filter((s, i) => s[1] === valid[i][1]).length,
    supportedDurationMs: supportedMs,
    coverage: extent > 0 ? Math.min(1, supportedMs / extent) : 0,
    timeWeightedMean: supportedMs ? weighted / supportedMs : null,
    minimum: valid.length ? valid.reduce((a, b) => Math.min(a, b[1]), Infinity) : null,
    maximum: valid.length ? valid.reduce((a, b) => Math.max(a, b[1]), -Infinity) : null,
  };
}
export function causalAlign(samples, targetTimes, { maxAgeMs = 5000 } = {}) {
  if (!finite(maxAgeMs) || maxAgeMs < 0) fail("invalid_max_age");
  const grouped = new Map();
  for (const s of samples) {
    const t = stamp(s.time);
    if (!finite(t) || !finite(s.value)) continue;
    if (grouped.has(t) && grouped.get(t) !== s.value) grouped.set(t, null);
    else if (!grouped.has(t)) grouped.set(t, s.value);
  }
  const points = [...grouped].sort((a, b) => a[0] - b[0]);
  return targetTimes.map((time) => {
    const t = stamp(time);
    if (!finite(t)) fail("invalid_target_time");
    let left = 0,
      right = points.length - 1,
      index = -1;
    while (left <= right) {
      const mid = Math.floor((left + right) / 2);
      if (points[mid][0] <= t) {
        index = mid;
        left = mid + 1;
      } else right = mid - 1;
    }
    if (index < 0 || points[index][1] === null || t - points[index][0] > maxAgeMs)
      return { time, value: null, observed: false, ageMs: null };
    return { time, value: points[index][1], observed: true, ageMs: t - points[index][0] };
  });
}
export function validateObservation(observation, ownerId) {
  if (!ownerId || observation.ownerId !== ownerId) fail("owner_mismatch");
  if (
    ["metric", "unit", "source"].some(
      (k) => typeof observation[k] !== "string" || !observation[k] || observation[k].length > 200,
    ) ||
    !observation.method ||
    !finite(stamp(observation.time)) ||
    !finite(stamp(observation.availableAt))
  )
    fail("observation_metadata_missing");
  if (units.has(observation.metric) && units.get(observation.metric) !== observation.unit)
    fail("unit_mismatch");
  if (observation.value !== null && !finite(observation.value)) fail("invalid_observation_value");
  if (
    ![
      "measured",
      "provider_estimated",
      "self_reported",
      "derived",
      "model_inferred",
      "provider_reported",
    ].includes(observation.method)
  )
    fail("invalid_observation_method");
  if (
    observation.method === "derived" &&
    (!observation.derivationVersion || !Array.isArray(observation.inputIds))
  )
    fail("derivation_missing");
  if (observation.method === "provider_estimated" && !observation.providerAlgorithm)
    fail("provider_algorithm_required");
  return true;
}
export function validateFeedback(feedback, ownerId) {
  if (feedback.ownerId !== ownerId) fail("owner_mismatch");
  if (
    !feedback.id ||
    !finite(stamp(feedback.time)) ||
    !finite(stamp(feedback.availableAt)) ||
    feedback.scale !== "moscorun-feedback-v1"
  )
    fail("feedback_metadata_missing");
  for (const key of [
    "rpe",
    "fatigue",
    "stressPerceived",
    "soreness",
    "pain",
    "sleepQuality",
    "hunger",
    "gastrointestinalDiscomfort",
    "motivation",
  ]) {
    const v = feedback[key];
    if (v !== undefined && v !== null && (!finite(v) || v < 0 || v > 10)) fail("feedback_out_of_range");
  }
  if (feedback.pain > 0 && !feedback.painLocation) fail("pain_location_required");
  if (feedback.notes !== undefined && (typeof feedback.notes !== "string" || feedback.notes.length > 2000))
    fail("invalid_feedback_notes");
  return true;
}
// Ordine totale e indipendente dall'ordine di ingresso: tempo, disponibilità, id e infine il contenuto.
// Serve sia a scegliere i record più recenti quando il contesto viene troncato sia a rendere stabile l'impronta.
const byTime = (key) => (a, b) =>
  stamp(a[key]) - stamp(b[key]) ||
  stamp(a.availableAt) - stamp(b.availableAt) ||
  String(a.id ?? "").localeCompare(String(b.id ?? "")) ||
  JSON.stringify(a).localeCompare(JSON.stringify(b));
export function buildContext(
  dataset,
  { ownerId, asOf, maxWorkouts = 20, maxObservations = 200, maxFeedback = 200 },
) {
  const at = stamp(asOf);
  if (dataset.ownerId !== ownerId || !ownerId) fail("owner_mismatch");
  if (
    dataset.schemaVersion !== 1 ||
    !finite(at) ||
    !Number.isInteger(maxWorkouts) ||
    maxWorkouts < 1 ||
    maxWorkouts > 100 ||
    ![maxObservations, maxFeedback].every((n) => Number.isInteger(n) && n >= 1 && n <= 1000)
  )
    fail("invalid_context_request");
  const available = (r) => finite(stamp(r.availableAt)) && stamp(r.availableAt) <= at;
  const all = dataset.workouts ?? [],
    observations = dataset.observations ?? [],
    feedback = dataset.feedback ?? [];
  for (const r of [...all, ...observations, ...feedback]) if (r.ownerId !== ownerId) fail("owner_mismatch");
  observations.forEach((o) => validateObservation(o, ownerId));
  feedback.forEach((f) => validateFeedback(f, ownerId));
  const eligible = all
    .filter((w) => available(w) && finite(stamp(w.endedAt)) && stamp(w.endedAt) <= at)
    .sort(byTime("startedAt"));
  const selected = eligible.slice(-maxWorkouts).reverse();
  const workouts = selected.map((w) => ({
    id: w.id,
    revision: w.revision,
    source: w.source,
    startedAt: w.startedAt,
    endedAt: w.endedAt,
    series: (w.series ?? [])
      .filter((s) => available(s) && units.has(s.metric) && units.get(s.metric) === s.unit)
      .sort(
        (a, b) =>
          a.metric.localeCompare(b.metric) || String(a.source ?? "").localeCompare(String(b.source ?? "")),
      )
      .map((s) => ({
        metric: s.metric,
        unit: s.unit,
        source: s.source ?? w.source,
        quality: auditSeries(
          s.samples.filter((p) => stamp(p.time) <= at),
          { start: w.startedAt, end: w.endedAt },
        ),
      })),
  }));
  const eligibleObservations = observations
    .filter((o) => available(o) && stamp(o.time) <= at && units.has(o.metric))
    .sort(byTime("time"));
  const eligibleFeedback = feedback.filter((f) => available(f) && stamp(f.time) <= at).sort(byTime("time"));
  const pick = (o, keys) => Object.fromEntries(keys.filter((k) => o[k] !== undefined).map((k) => [k, o[k]]));
  const selectedObservations = eligibleObservations
    .slice(-maxObservations)
    .map((o) =>
      pick(o, [
        "id",
        "metric",
        "unit",
        "value",
        "time",
        "availableAt",
        "source",
        "method",
        "providerAlgorithm",
        "derivationVersion",
        "inputIds",
        "uncertainty",
      ]),
    );
  const selectedFeedback = eligibleFeedback
    .slice(-maxFeedback)
    .map((o) =>
      pick(o, [
        "id",
        "workoutId",
        "time",
        "availableAt",
        "scale",
        "rpe",
        "fatigue",
        "stressPerceived",
        "soreness",
        "pain",
        "painLocation",
        "sleepQuality",
        "hunger",
        "gastrointestinalDiscomfort",
        "motivation",
        "notes",
        "outcome",
        "modificationReason",
      ]),
    );
  const result = {
    schemaVersion: 1,
    analysisVersion: "moscorun-baseline-v1",
    ownerId,
    asOf: new Date(at).toISOString(),
    workouts,
    observations: selectedObservations,
    feedback: selectedFeedback,
    completeness: {
      eligibleWorkouts: eligible.length,
      returnedWorkouts: selected.length,
      truncated: eligible.length > selected.length,
      excludedUnavailableWorkouts: all.length - eligible.length,
      eligibleObservations: eligibleObservations.length,
      returnedObservations: selectedObservations.length,
      eligibleFeedback: eligibleFeedback.length,
      returnedFeedback: selectedFeedback.length,
      unmappedObservations: observations.filter((o) => !units.has(o.metric)).length,
    },
    interpretation: {
      feedbackText: "untrusted_user_data",
      providerMetrics: "estimates_are_not_measurements",
      causality: "descriptive_only",
      missingData: "explicit_nulls_and_quality",
      location: "gps_not_included",
    },
  };
  return { ...result, contentSha256: createHash("sha256").update(JSON.stringify(result)).digest("hex") };
}
export function chronologicalSplit(workouts, { testFraction = 0.2 } = {}) {
  if (!finite(testFraction) || testFraction <= 0 || testFraction >= 1) fail("invalid_split");
  const ordered = [...workouts].sort((a, b) => stamp(a.startedAt) - stamp(b.startedAt));
  if (ordered.some((w) => !w.id || !finite(stamp(w.startedAt)))) fail("invalid_workout");
  if (new Set(ordered.map((w) => w.ownerId)).size > 1) fail("use_athlete_holdout_for_multiple_athletes");
  if (new Set(ordered.map((w) => w.id)).size !== ordered.length) fail("duplicate_workout_id");
  const count = ordered.length < 2 ? 0 : Math.max(1, Math.ceil(ordered.length * testFraction));
  return { train: count ? ordered.slice(0, -count) : ordered, test: count ? ordered.slice(-count) : [] };
}
export function athleteHoldout(workouts, heldOutOwnerId) {
  if (!heldOutOwnerId || workouts.some((w) => !w.ownerId)) fail("invalid_athlete_split");
  return {
    train: workouts.filter((w) => w.ownerId !== heldOutOwnerId),
    test: workouts.filter((w) => w.ownerId === heldOutOwnerId),
  };
}
export function persistenceReplay(samples, { horizonMs = 30000, maxAgeMs = 5000 } = {}) {
  if (!finite(horizonMs) || horizonMs <= 0) fail("invalid_horizon");
  const valid = samples.filter((s) => finite(stamp(s.time)) && finite(s.value));
  const predictions = causalAlign(
    valid,
    valid.map((s) => stamp(s.time) - horizonMs),
    { maxAgeMs },
  );
  const errors = valid
    .map((s, i) => (predictions[i].observed ? s.value - predictions[i].value : null))
    .filter(finite);
  return {
    model: "persistence",
    horizonMs,
    evaluated: errors.length,
    excluded: valid.length - errors.length,
    mae: errors.length ? errors.reduce((s, e) => s + Math.abs(e), 0) / errors.length : null,
    rmse: errors.length ? Math.sqrt(errors.reduce((s, e) => s + e * e, 0) / errors.length) : null,
  };
}
